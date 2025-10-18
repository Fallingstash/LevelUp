using DriverDeploy.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DriverDeploy.Shared.Services {
  public class DriverMatcher {
    public static List<DriverPackage> FindMatchingDrivers(
        DeviceDescriptor device,
        List<DriverPackage> availableDrivers) {
      var matches = new List<DriverPackage>();

      foreach (var driver in availableDrivers) {
        if (IsDriverCompatible(device, driver)) {
          matches.Add(driver);
        }
      }

      return matches;
    }

    private static bool IsDriverCompatible(DeviceDescriptor device, DriverPackage driver) {
      if (driver.SupportedHardware.Any()) {
        foreach (var hardware in driver.SupportedHardware) {
          foreach (var deviceHardwareId in device.HardwareIds) {
            if (deviceHardwareId.IndexOf(hardware.HardwareID, StringComparison.OrdinalIgnoreCase) >= 0) {
              return true;
            }
          }
        }
      }

      return HeuristicMatch(device, driver);
    }

    private static bool HeuristicMatch(DeviceDescriptor device, DriverPackage driver) {
      var deviceName = device.Name.ToLowerInvariant();
      var driverName = driver.Name.ToLowerInvariant();
      var manufacturer = device.Manufacturer.ToLowerInvariant();

      var graphicsKeywords = new[] { "nvidia", "geforce", "radeon", "amd", "intel graphics", "graphics" };
      var audioKeywords = new[] { "realtek", "audio", "sound", "hd audio" };
      var networkKeywords = new[] { "intel", "ethernet", "network", "wifi", "wireless" };

      if (device.Category.Contains("Display") || device.Category.Contains("GPU")) {
        return graphicsKeywords.Any(keyword =>
            driverName.Contains(keyword) || deviceName.Contains(keyword));
      }

      if (device.Category.Contains("Audio") || device.Category.Contains("Microphone")) {
        return audioKeywords.Any(keyword =>
            driverName.Contains(keyword) || deviceName.Contains(keyword));
      }

      if (device.Category.Contains("Network") || device.Category.Contains("Net")) {
        return networkKeywords.Any(keyword =>
            driverName.Contains(keyword) || deviceName.Contains(keyword));
      }

      if (!string.IsNullOrEmpty(manufacturer) &&
          driverName.Contains(manufacturer)) {
        return true;
      }

      return false;
    }

    public static bool NeedsUpdate(DeviceDescriptor device, DriverPackage latestDriver) {
      if (string.IsNullOrEmpty(device.DriverVersion) || string.IsNullOrEmpty(latestDriver.Version))
        return true;

      try {
        var currentVersion = new Version(device.DriverVersion);
        var availableVersion = new Version(latestDriver.Version);
        return availableVersion > currentVersion;
      }
      catch {
        return true;
      }
    }
  }
}