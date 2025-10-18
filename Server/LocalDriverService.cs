using DriverDeploy.Shared.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace DriverDeploy.Server.Services {
  public class LocalDriverService {
    private readonly HttpClient _httpClient;
    private readonly string _repositoryBaseUrl;

    private RepoDriverMapping _cachedMapping;
    private DateTime _lastUpdateTime;

        public LocalDriverService(string repositoryUrl = "http://localhost:5000")
        {
            _httpClient = new HttpClient();

            if (!repositoryUrl.StartsWith("http://") && !repositoryUrl.StartsWith("https://"))
            {
                repositoryUrl = "http://" + repositoryUrl;
            }

            _repositoryBaseUrl = repositoryUrl.TrimEnd('/');

            try
            {
                _httpClient.BaseAddress = new Uri(_repositoryBaseUrl);
                Console.WriteLine($"✅ BaseAddress установлен: {_repositoryBaseUrl}");
            }
            catch (UriFormatException ex)
            {
                Console.WriteLine($"⚠️ Ошибка URI: {ex.Message}, используем localhost");
                _repositoryBaseUrl = "http://localhost:5000";
                _httpClient.BaseAddress = new Uri(_repositoryBaseUrl);
            }

            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<RepoDriverMapping> LoadDriverMappingAsync()
        {
            try
            {
                Console.WriteLine($"🌐 Запрашиваем драйверы из: {_repositoryBaseUrl}/drivers.json");

                _httpClient.BaseAddress = null;
                var fullUrl = $"{_repositoryBaseUrl}/drivers.json";

                Console.WriteLine($"🔧 Полный URL: {fullUrl}");

                var response = await _httpClient.GetAsync(fullUrl);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"✅ JSON получен, размер: {json.Length} символов");

                var mapping = JsonConvert.DeserializeObject<RepoDriverMapping>(json);

                if (mapping != null && mapping.Drivers?.Count > 0)
                {
                    _cachedMapping = mapping;
                    _lastUpdateTime = DateTime.Now;
                    Console.WriteLine($"✅ Успешно загружено {mapping.Drivers.Count} драйверов из репозитория");
                    return mapping;
                }
                else
                {
                    Console.WriteLine("⚠️ JSON загружен, но драйверы не найдены, используем fallback");
                    return CreateFallBackDrivers();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка загрузки JSON: {ex.Message}");
                Console.WriteLine($"🔄 Используем fallback драйверы");
                return CreateFallBackDrivers();
            }
        }

        private RepoDriverMapping CreateFallBackDrivers()
        {
            return new RepoDriverMapping
            {
                Drivers = new List<RepoDriverEntry> {
                new RepoDriverEntry {
                    Name = "NVIDIA Graphics Driver (Demo)",
                    Version = "456.71",
                    Description = "Демо-драйвер для видеокарт NVIDIA",
                    HardwareIds = new[] { "PCI\\VEN_10DE", "PCI\\VEN_10DE&DEV_1C03", "PCI\\VEN_10DE&DEV_1C82" },
                    Url = "https://us.download.nvidia.com/Windows/456.71/456.71-desktop-win10-win11-64bit-international-whql.exe",
                    InstallArgs = "/S /quiet /noreboot",
                    Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
                },
                new RepoDriverEntry {
                    Name = "Realtek Audio (Demo)",
                    Version = "6.0.1.1234",
                    Description = "Демо-драйвер для звуковых карт Realtek",
                    HardwareIds = new[] { "HDAUDIO\\FUNC_01", "HDAUDIO\\VEN_10EC", "VEN_10EC&DEV_0662" },
                    Url = "https://www.realtek.com/downloads/files/Realtek_Audio_Demo.exe",
                    InstallArgs = "/S",
                    Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
                },
                new RepoDriverEntry {
                    Name = "Intel Network (Demo)",
                    Version = "12.18.9.0",
                    Description = "Демо-драйвер для сетевых карт Intel",
                    HardwareIds = new[] { "PCI\\VEN_8086", "PCI\\VEN_8086&DEV_15BE", "PCI\\VEN_8086&DEV_15F2" },
                    Url = "https://downloadmirror.intel.com/25016/eng/PROWin64.exe",
                    InstallArgs = "/S /quiet",
                    Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
                }
            }
            };
        }

        public RepoDriverEntry? FindDriverForDevice(DeviceDescriptor device)
        {
            if (_cachedMapping?.Drivers == null)
            {
                Console.WriteLine("⚠️ Кэш драйверов пуст, загружаем...");
                var loadTask = LoadDriverMappingAsync();
                loadTask.Wait();
                _cachedMapping = loadTask.Result;
            }

            foreach (var driver in _cachedMapping.Drivers)
            {
                if (IsDeviceCompatibleWithDriver(device, driver))
                {
                    if (NeedsDriverUpdate(device, driver))
                    {
                        Console.WriteLine($"🔄 Найден драйвер для обновления: {driver.Name}");
                        return driver;
                    }
                    else
                    {
                        Console.WriteLine($"✅ Драйвер актуален: {driver.Name}");
                    }
                }
            }
            return null;
        }

        private bool NeedsDriverUpdate(DeviceDescriptor device, RepoDriverEntry driver)
        {
            if (string.IsNullOrEmpty(device.DriverVersion) ||
                device.DriverVersion == "Unknown" ||
                device.DriverVersion == "0.0.0.0")
            {
                Console.WriteLine($"⚠️ У устройства нет драйвера, требуется установка");
                return true;
            }

            if (string.IsNullOrEmpty(driver.Version))
            {
                Console.WriteLine($"⚠️ В репозитории нет версии, устанавливаем");
                return true;
            }

            if (device.Manufacturer.Contains("Microsoft") &&
                !device.DriverVersion.StartsWith("0.") &&
                !device.DriverVersion.StartsWith("1."))
            {
                Console.WriteLine($"🔧 Пропускаем обновление стандартного Microsoft драйвера");
                return false;
            }

            try
            {
                var currentVersion = new Version(NormalizeVersion(device.DriverVersion));
                var repoVersion = new Version(NormalizeVersion(driver.Version));

                bool needsUpdate = repoVersion > currentVersion;
                Console.WriteLine($"🔍 Сравнение версий: {device.DriverVersion} -> {driver.Version} = {needsUpdate}");
                return needsUpdate;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка сравнения версий: {ex.Message}, устанавливаем");
                return true;
            }
        }

        private string NormalizeVersion(string version)
        {
            var normalized = System.Text.RegularExpressions.Regex.Replace(version, @"[^\d\.]", "");

            if (string.IsNullOrEmpty(normalized)) return "0.0.0.0";

            var parts = normalized.Split('.');
            if (parts.Length < 4)
            {
                var list = parts.ToList();
                while (list.Count < 4) list.Add("0");
                normalized = string.Join(".", list);
            }

            return normalized;
        }

        private bool IsDeviceCompatibleWithDriver(DeviceDescriptor device, RepoDriverEntry driver) {
      if (device.HardwareIds == null || driver.HardwareIds == null)
        return false;

      foreach (var deviceHwId in device.HardwareIds) {
        foreach (var driverHwId in driver.HardwareIds) {
          if (deviceHwId.IndexOf(driverHwId, StringComparison.OrdinalIgnoreCase) >= 0) {
            return true;
          }
        }
      }

      return false;
    }

        public DriverPackage ConvertToDriverPackage(RepoDriverEntry repoEntry)
        {
            string finalUrl = repoEntry.Url;
            if (!repoEntry.Url.StartsWith("http://") && !repoEntry.Url.StartsWith("https://"))
            {
                finalUrl = $"{_repositoryBaseUrl}/{repoEntry.Url.TrimStart('/')}";
            }

            return new DriverPackage
            {
                Name = repoEntry.Name,
                Version = repoEntry.Version,
                Description = repoEntry.Description,
                Url = finalUrl,
                InstallArgs = repoEntry.InstallArgs,
                Sha256 = repoEntry.Sha256,
                FileName = System.IO.Path.GetFileName(repoEntry.Url)
            };
        }

        public async Task RefreshCacheIfNeededAsync() {
      if (_cachedMapping == null || DateTime.Now - _lastUpdateTime > TimeSpan.FromMinutes(5)) {
        await LoadDriverMappingAsync();
      }
    }
  }
}