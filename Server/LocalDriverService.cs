// DriverDeploy.Server/Services/LocalDriverService.cs
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

    // Кэш mapping'а драйверов
    private RepoDriverMapping _cachedMapping;
    private DateTime _lastUpdateTime;

    public LocalDriverService(string repositoryUrl = "http://localhost:5000") {
      _httpClient = new HttpClient();
      _repositoryBaseUrl = repositoryUrl.TrimEnd('/');
      _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Загружает mapping драйверов из репозитория
    /// </summary>
    public async Task<RepoDriverMapping> LoadDriverMappingAsync() {
      try {
        Console.WriteLine($"🌐 Запрашиваем драйверы из: {_repositoryBaseUrl}/drivers.json");

        var json = await _httpClient.GetStringAsync($"{_repositoryBaseUrl}/drivers.json");

        // Логируем полученный JSON (первые 500 символов)
        Console.WriteLine($"📄 Получен JSON (первые 500 символов): {json.Substring(0, Math.Min(500, json.Length))}...");

        var mapping = JsonConvert.DeserializeObject<RepoDriverMapping>(json);

        if (mapping != null) {
          _cachedMapping = mapping;
          _lastUpdateTime = DateTime.Now;
          Console.WriteLine($"✅ Успешно десериализовано {mapping.Drivers?.Count} драйверов");
        } else {
          Console.WriteLine("⚠️ Десериализация вернула null");
        }

        return mapping ?? new RepoDriverMapping();
      }
      catch (Exception ex) {
        Console.WriteLine($"❌ Критическая ошибка загрузки драйверов: {ex}");
        throw new Exception($"Не удалось загрузить drivers.json из репозитория: {ex.Message}");
      }
    }

    /// <summary>
    /// Находит подходящий драйвер для устройства по HardwareID
    /// </summary>
    public RepoDriverEntry? FindDriverForDevice(DeviceDescriptor device) {
      if (_cachedMapping?.Drivers == null)
        return null;

      foreach (var driver in _cachedMapping.Drivers) {
        if (IsDeviceCompatibleWithDriver(device, driver)) {
          return driver;
        }
      }

      return null;
    }

    /// <summary>
    /// Проверяет совместимость устройства с драйвером по HardwareID
    /// </summary>
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

    /// <summary>
    /// Преобразует запись из репозитория в DriverPackage для отправки агенту
    /// </summary>
    public DriverPackage ConvertToDriverPackage(RepoDriverEntry repoEntry) {
      return new DriverPackage {
        Name = repoEntry.Name,
        Version = repoEntry.Version,
        Description = repoEntry.Description,
        Url = $"{_repositoryBaseUrl}/{repoEntry.Url.TrimStart('/')}",
        InstallArgs = repoEntry.InstallArgs,
        Sha256 = repoEntry.Sha256,
        FileName = System.IO.Path.GetFileName(repoEntry.Url)
      };
    }

    /// <summary>
    /// Обновляет кэш если прошло больше 5 минут
    /// </summary>
    public async Task RefreshCacheIfNeededAsync() {
      if (_cachedMapping == null || DateTime.Now - _lastUpdateTime > TimeSpan.FromMinutes(5)) {
        await LoadDriverMappingAsync();
      }
    }
  }
}