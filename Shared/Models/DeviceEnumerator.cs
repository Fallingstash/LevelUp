using DriverDeploy.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management; // System.Management (добавьте пакет для .NET Core: System.Management)
using System.Text.RegularExpressions;

namespace DriverDeploy.Agent
{
    public static class DeviceEnumerator
    {
        /// <summary>
        /// Возвращает все PnP-устройства с категорией и версией драйвера.
        /// </summary>
        public static List<DeviceDescriptor> GetAllDevices()
        {
            // 1) Читаем все PnP-устройства (в т.ч. Mic = AudioEndpoint, GPU = Display и т.д.)
            var devices = QueryPnPEntities();

            // 2) Подтягиваем версии драйверов из Win32_PnPSignedDriver
            var driverMap = QuerySignedDrivers(); // ключ: DeviceID (PNPDeviceID), значение: DriverVersion

            foreach (var d in devices)
            {
                if (driverMap.TryGetValue(d.PnpDeviceId, out var ver))
                    d.DriverVersion = ver;
            }

            // 3) Укрупнённая нормализация категории для UX (опционально)
            foreach (var d in devices)
                d.Category = NormalizeCategory(d.Category, d.Name);

            return devices;
        }

        private static List<DeviceDescriptor> QueryPnPEntities()
        {
            var result = new List<DeviceDescriptor>();

            // Берём только устройства с валидной конфигурацией (ConfigManagerErrorCode = 0)
            using var searcher = new ManagementObjectSearcher(
              "SELECT Name,PNPDeviceID,PNPClass,Manufacturer,HardwareID,ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0");

            foreach (ManagementObject mo in searcher.Get())
            {
                var name = (mo["Name"] as string) ?? string.Empty;
                var pnpId = (mo["PNPDeviceID"] as string) ?? string.Empty;
                var pnpClass = (mo["PNPClass"] as string) ?? string.Empty; // примеры: 'Display', 'Media', 'AudioEndpoint', 'Net'
                var mfg = (mo["Manufacturer"] as string) ?? string.Empty;

                string[] hwids = Array.Empty<string>();
                if (mo["HardwareID"] is string[] arr && arr.Length > 0)
                    hwids = arr;

                result.Add(new DeviceDescriptor
                {
                    Name = name,
                    Category = pnpClass,
                    Manufacturer = mfg,
                    DriverVersion = string.Empty, // заполним позже
                    PnpDeviceId = pnpId,
                    HardwareIds = hwids
                });
            }
            return result;
        }

        private static Dictionary<string, string> QuerySignedDrivers()
        {
            // DeviceID в Win32_PnPSignedDriver == PNPDeviceID в Win32_PnPEntity
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using var searcher = new ManagementObjectSearcher(
              "SELECT DeviceID, DriverVersion FROM Win32_PnPSignedDriver");

            foreach (ManagementObject mo in searcher.Get())
            {
                var devId = (mo["DeviceID"] as string) ?? string.Empty;
                var ver = (mo["DriverVersion"] as string) ?? string.Empty;

                if (!string.IsNullOrEmpty(devId) && !map.ContainsKey(devId))
                    map[devId] = ver;
            }
            return map;
        }

        /// <summary>
        /// Приводит PNPClass к читаемой категории. Для микрофона PNPClass обычно 'AudioEndpoint' и в имени часто есть 'Microphone'.
        /// </summary>
        private static string NormalizeCategory(string pnpClass, string name)
        {
            if (string.IsNullOrEmpty(pnpClass))
            {
                // Пытаемся угадать по имени
                if (name.Contains("Microphone", StringComparison.OrdinalIgnoreCase)) return "Microphone";
                if (name.Contains("Camera", StringComparison.OrdinalIgnoreCase) || name.Contains("Webcam", StringComparison.OrdinalIgnoreCase)) return "Camera";
                return "Other";
            }

            // Базовые сопоставления
            return pnpClass switch
            {
                "Display" => "GPU",
                "Media" => "Audio (Codec/Controller)",
                "AudioEndpoint" => name.Contains("Microphone", StringComparison.OrdinalIgnoreCase) ? "Microphone" : "Audio Endpoint",
                "Net" => "Network",
                "HIDClass" => "HID / Input",
                "Bluetooth" => "Bluetooth",
                "USB" => "USB",
                "Image" => "Camera / Imaging",
                "Battery" => "Battery",
                "Keyboard" => "Keyboard",
                "Mouse" => "Mouse",
                "System" => "System",
                "SCSIAdapter" => "Storage Controller",
                "Ports" => "Ports (COM/LPT)",
                _ => pnpClass
            };
        }
    }
}
