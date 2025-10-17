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
            var realDevices = QueryPnPEntities();
            var driverMap = QuerySignedDrivers();

            // ДОБАВЛЯЕМ ТЕСТОВЫЕ УСТРОЙСТВА ДЛЯ ДЕМО
            // ДОБАВЛЯЕМ ТЕСТОВЫЕ УСТРОЙСТВА ДЛЯ ДЕМО
            var demoDevices = new List<DeviceDescriptor> {
    new DeviceDescriptor {
        Name = "TEST NVIDIA Graphics Card [DEMO]",
        Category = "GPU",
        Manufacturer = "NVIDIA Corporation",
        DriverVersion = "1.0.0.0", // ← УСТАРЕВШАЯ ВЕРСИЯ
        PnpDeviceId = "TEST_DEVICE_001",
        HardwareIds = new[] { "PCI\\VEN_10DE&DEV_1C03", "PCI\\VEN_10DE&DEV_1C82" }
    },
    new DeviceDescriptor {
        Name = "TEST Realtek Audio [DEMO]",
        Category = "Audio Endpoint",
        Manufacturer = "Realtek",
        DriverVersion = "", // ← ПУСТАЯ ВЕРСИЯ (вообще нет драйвера)
        PnpDeviceId = "TEST_DEVICE_002",
        HardwareIds = new[] { "HDAUDIO\\FUNC_01&VEN_10EC", "VEN_10EC&DEV_0662" }
    },
    new DeviceDescriptor {
        Name = "TEST Intel Network [DEMO]",
        Category = "Network",
        Manufacturer = "Intel",
        DriverVersion = "0.0.0.0", // ← НУЛЕВАЯ ВЕРСИЯ
        PnpDeviceId = "TEST_DEVICE_003",
        HardwareIds = new[] { "PCI\\VEN_8086&DEV_15BE", "PCI\\VEN_8086&DEV_15F2" }
    }
};

            realDevices.AddRange(demoDevices);

            foreach (var d in realDevices)
            {
                if (driverMap.TryGetValue(d.PnpDeviceId, out var ver))
                    d.DriverVersion = ver;
            }

            return realDevices;
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
