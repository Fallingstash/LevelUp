using System;
using System.Collections.Generic;

namespace DriverDeploy.Shared.Models
{
    public class MachineInfo
    {
        public string? MachineName { get; set; } = string.Empty;
        public string? IpAddress { get; set; } = string.Empty;
        public string? Status { get; set; } = "Unknown";
        public DateTime LastSeen { get; set; } = DateTime.Now;

        // Новые свойства для информации о системе
        public string OSVersion { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;
        public List<DriverInfo> InstalledDrivers { get; set; } = new();
        public List<DriverInfo> OutdatedDrivers { get; set; } = new();
        public bool IsOnline { get; set; }
    }
}