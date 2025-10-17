namespace DriverDeploy.Shared.Models
{
    /// <summary>
    /// Универсальное описание PnP-устройства в системе.
    /// Используется для получения списка устройств (GPU, микрофоны, сеть и т.д.)
    /// </summary>
    public class DeviceDescriptor
    {
        public string Name { get; set; } = string.Empty;           // Имя устройства
        public string Category { get; set; } = string.Empty;       // Категория/PNPClass
        public string Manufacturer { get; set; } = string.Empty;   // Производитель
        public string DriverVersion { get; set; } = string.Empty;  // Версия драйвера
        public string PnpDeviceId { get; set; } = string.Empty;    // Уникальный идентификатор устройства
        public string[] HardwareIds { get; set; } = Array.Empty<string>(); // Массив HWID
    }
}
