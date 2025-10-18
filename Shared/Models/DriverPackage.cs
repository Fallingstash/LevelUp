using System;
using System.Collections.Generic;

namespace DriverDeploy.Shared.Models {
  public class DriverPackage {
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
    public string InstallArgs { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;

    public List<HardwareId> SupportedHardware { get; set; } = new();
  }

  public class HardwareId {
    public string HardwareID { get; set; } = string.Empty;
  }
}