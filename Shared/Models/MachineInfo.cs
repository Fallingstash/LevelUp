using System;
using System.Collections.Generic;

namespace DriverDeploy.Shared.Models
{
    public class MachineInfo
    {
        public string? MachineName { get; set; } = string.Empty;
        public string? IpAddress { get; set; } = string.Empty;
        public string? Status { get; set; } = "Unknown";

        public string OSVersion { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
    }
}