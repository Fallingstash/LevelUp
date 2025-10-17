using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DriverDeploy.Shared.Models {
  public class DriverInfo {
    public string DeviceName { get; set; } = string.Empty;
    public string DriverVersion { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool NeedsUpdate { get; set; }
  }
}
