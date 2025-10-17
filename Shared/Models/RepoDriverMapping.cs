using System.Collections.Generic;

namespace DriverDeploy.Shared.Models {
  public class RepoDriverMapping {
    public List<RepoDriverEntry> Drivers { get; set; } = new List<RepoDriverEntry>();
  }

  public class RepoDriverEntry {
    public string[] HardwareIds { get; set; } = System.Array.Empty<string>();
    public string Url { get; set; } = string.Empty;
    public string InstallArgs { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
  }
}