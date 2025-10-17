using DriverDeploy.Agent.Services;
using DriverDeploy.Shared.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DriverDeploy.Agent {
  class Program {
    private static List<DriverInfo> _systemDrivers = new();
    private static DriverInstallerService _driverInstaller;

    static async Task Main(string[] args) {
      // Инициализация сервиса установки
      _driverInstaller = new DriverInstallerService();

      var builder = WebApplication.CreateBuilder(args);
      var app = builder.Build();

      // Эндпоинт для проверки доступности
      app.MapGet("/api/ping", () =>
      {
        Console.WriteLine($"✅ Получен ping запрос от клиента");
        return new MachineInfo {
          MachineName = Environment.MachineName,
          IpAddress = GetLocalIPAddress(),
          Status = "Online",
          OSVersion = Environment.OSVersion.VersionString,
          Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86",
          IsOnline = true
        };
      });

      // Эндпоинт для получения списка драйверов
      app.MapGet("/api/drivers", () =>
      {
        Console.WriteLine($"📦 Запрос списка драйверов");
        if (!_systemDrivers.Any()) {
          _systemDrivers = ScanSystemDrivers();
        }
        return _systemDrivers;
      });

      // Эндпоинт для установки драйвера
      app.MapPost("/api/drivers/install", async (DriverPackage driverPackage) =>
      {
        Console.WriteLine($"🔧 Запрос на установку драйвера: {driverPackage.Name}");
        Console.WriteLine($"📥 URL: {driverPackage.Url}");
        Console.WriteLine($"⚙️ Аргументы: {driverPackage.InstallArgs}");

        try {
          var result = await _driverInstaller.InstallDriverAsync(driverPackage);

          // Логируем результат
          if (result.Success) {
            Console.WriteLine($"✅ Установка завершена успешно: {result.Message}");
          } else {
            Console.WriteLine($"❌ Установка завершена с ошибкой: {result.Message}");
          }

          return Results.Ok(result);
        }
        catch (Exception ex) {
          var errorResult = new InstallationResult {
            Success = false,
            Message = $"❌ Критическая ошибка: {ex.Message}",
            DriverName = driverPackage.Name,
            MachineName = Environment.MachineName,
            Timestamp = DateTime.Now
          };

          Console.WriteLine($"💥 Критическая ошибка: {ex}");
          return Results.Ok(errorResult);
        }
      });

      // Эндпоинт для проверки обновлений
      app.MapGet("/api/drivers/outdated", () =>
      {
        Console.WriteLine($"🔍 Проверка устаревших драйверов");
        var outdated = FindOutdatedDrivers();
        return Results.Ok(outdated);
      });

      // Эндпоинт для получения списка устройств
      app.MapGet("/api/devices", () =>
      {
        var devices = DeviceEnumerator.GetAllDevices();
        Console.WriteLine($"🧭 Запрос списка устройств. Найдено: {devices.Count}");
        return Results.Ok(devices);
      });

      // Новый эндпоинт для проверки здоровья агента
      app.MapGet("/api/health", () =>
      {
        return Results.Ok(new {
          status = "healthy",
          machineName = Environment.MachineName,
          os = Environment.OSVersion.VersionString,
          timestamp = DateTime.Now,
          version = "1.0.0"
        });
      });

      // Новый эндпоинт для получения информации о диске (для отладки)
      app.MapGet("/api/system/info", () =>
      {
        var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
        return Results.Ok(new {
          totalSpaceGB = Math.Round(drive.TotalSize / (1024.0 * 1024 * 1024), 2),
          freeSpaceGB = Math.Round(drive.AvailableFreeSpace / (1024.0 * 1024 * 1024), 2),
          tempPath = Path.GetTempPath(),
          isAdmin = IsRunningAsAdministrator()
        });
      });

      try {
        Console.WriteLine("🚀 Запуск DriverDeploy Agent...");
        Console.WriteLine($"📍 Агент слушает на: http://0.0.0.0:8081");
        Console.WriteLine($"💻 Имя машины: {Environment.MachineName}");
        Console.WriteLine($"🖥️ ОС: {Environment.OSVersion.VersionString}");
        Console.WriteLine($"🔧 Готов к работе!");

        await app.RunAsync("http://0.0.0.0:8080");
      }
      catch (Exception ex) {
        Console.WriteLine($"💥 Критическая ошибка при запуске: {ex}");
      }
      finally {
        _driverInstaller?.Cleanup();
      }
    }

    static string GetLocalIPAddress() {
      var host = Dns.GetHostEntry(Dns.GetHostName());
      foreach (var ip in host.AddressList) {
        if (ip.AddressFamily == AddressFamily.InterNetwork) {
          return ip.ToString();
        }
      }
      return "Unknown";
    }

    static List<DriverInfo> ScanSystemDrivers() {
      // Заглушка - в реальной реализации здесь будет сканирование через WMI
      return new List<DriverInfo>
      {
                new DriverInfo { DeviceName = "NVIDIA GeForce GTX 1060", DriverVersion = "456.71", Provider = "NVIDIA" },
                new DriverInfo { DeviceName = "Realtek Audio", DriverVersion = "6.0.1.1234", Provider = "Realtek" }
            };
    }

    static List<DriverInfo> FindOutdatedDrivers() {
      // Заглушка - в реальной реализации здесь будет проверка версий
      return new List<DriverInfo>
      {
                new DriverInfo { DeviceName = "NVIDIA GeForce GTX 1060", DriverVersion = "456.71", Provider = "NVIDIA", NeedsUpdate = true }
            };
    }

    static bool IsRunningAsAdministrator() {
      try {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
      }
      catch {
        return false;
      }
    }
  }
}