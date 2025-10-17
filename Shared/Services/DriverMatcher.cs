using DriverDeploy.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DriverDeploy.Shared.Services {
  public class DriverMatcher {
    /// <summary>
    /// Находит подходящие драйверы для устройства на основе HardwareID
    /// </summary>
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

    /// <summary>
    /// Проверяет совместимость драйвера с устройством
    /// </summary>
    private static bool IsDriverCompatible(DeviceDescriptor device, DriverPackage driver) {
      // Если у драйвера есть конкретные HardwareID, проверяем точное совпадение
      if (driver.SupportedHardware.Any()) {
        foreach (var hardware in driver.SupportedHardware) {
          foreach (var deviceHardwareId in device.HardwareIds) {
            if (deviceHardwareId.IndexOf(hardware.HardwareID, StringComparison.OrdinalIgnoreCase) >= 0) {
              return true;
            }
          }
        }
      }

      // Если точных HardwareID нет, используем эвристический анализ
      return HeuristicMatch(device, driver);
    }

    /// <summary>
    /// Эвристическое сопоставление по названию и производителю
    /// </summary>
    private static bool HeuristicMatch(DeviceDescriptor device, DriverPackage driver) {
      var deviceName = device.Name.ToLowerInvariant();
      var driverName = driver.Name.ToLowerInvariant();
      var manufacturer = device.Manufacturer.ToLowerInvariant();

      // Ключевые слова для разных категорий устройств
      var graphicsKeywords = new[] { "nvidia", "geforce", "radeon", "amd", "intel graphics", "graphics" };
      var audioKeywords = new[] { "realtek", "audio", "sound", "hd audio" };
      var networkKeywords = new[] { "intel", "ethernet", "network", "wifi", "wireless" };

      // Проверяем категорию устройства
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

      // Общая проверка по производителю
      if (!string.IsNullOrEmpty(manufacturer) &&
          driverName.Contains(manufacturer)) {
        return true;
      }

      return false;
    }

    /// <summary>
    /// Определяет, требуется ли обновление драйвера
    /// </summary>
    public static bool NeedsUpdate(DeviceDescriptor device, DriverPackage latestDriver) {
      if (string.IsNullOrEmpty(device.DriverVersion) || string.IsNullOrEmpty(latestDriver.Version))
        return true;

      try {
        var currentVersion = new Version(device.DriverVersion);
        var availableVersion = new Version(latestDriver.Version);
        return availableVersion > currentVersion;
      }
      catch {
        // Если не удалось распарсить версии, считаем что обновление требуется
        return true;
      }
    }
  }
}