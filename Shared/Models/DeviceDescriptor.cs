namespace DriverDeploy.Shared.Models
{
    public class DeviceDescriptor
    {
        public string Name { get; set; } = string.Empty;       
        public string Category { get; set; } = string.Empty;      
        public string Manufacturer { get; set; } = string.Empty;   
        public string DriverVersion { get; set; } = string.Empty;  
        public string PnpDeviceId { get; set; } = string.Empty;    
        public string[] HardwareIds { get; set; } = Array.Empty<string>(); 

        public bool NeedsUpdate { get; set; }
    }
}
