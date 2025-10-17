namespace DriverDeploy.Shared.Models
{
    public class InstallationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}