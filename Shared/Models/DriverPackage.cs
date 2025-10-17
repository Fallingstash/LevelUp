using System;
using System.Collections.Generic;

namespace DriverDeploy.Shared.Models {
  public class DriverPackage {
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Новые поля для работы с репозиторием
    public string Url { get; set; } = string.Empty;
    public string InstallArgs { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;

    // Старые поля (можно удалить, но оставим для совместимости)
    public List<HardwareId> SupportedHardware { get; set; } = new();
    public string FilePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
  }

  public class HardwareId {
    public string Vendor { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
    public string HardwareID { get; set; } = string.Empty;
  }
}