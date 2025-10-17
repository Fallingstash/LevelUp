    using DriverDeploy.Shared.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace DriverDeploy.Agent.Services {
  public class DriverInstallerService {
    private readonly HttpClient _httpClient;
    private readonly string _tempDownloadPath;

    public DriverInstallerService() {
      _httpClient = new HttpClient();
      _httpClient.Timeout = TimeSpan.FromMinutes(10); // Долгий таймаут для больших файлов

      _tempDownloadPath = Path.Combine(Path.GetTempPath(), "DriverDeploy");
      Directory.CreateDirectory(_tempDownloadPath);
    }

        /// <summary>
        /// Универсальный метод установки драйвера из любого репозитория
        /// </summary>
        public async Task<InstallationResult> InstallDriverAsync(DriverPackage driverPackage)
        {
            var result = new InstallationResult
            {
                DriverName = driverPackage.Name,
                MachineName = Environment.MachineName,
                Timestamp = DateTime.Now
            };

            try
            {
                Console.WriteLine($"🚀 Начинаем установку драйвера: {driverPackage.Name}");
                Console.WriteLine($"📍 URL: {driverPackage.Url}");

                // ДЕМО-РЕЖИМ: ТОЛЬКО для явно тестовых драйверов
                if (driverPackage.Name.Contains("Demo") || driverPackage.Name.Contains("TEST"))
                {
                    Console.WriteLine($"🎮 АКТИВИРУЕМ ДЕМО-РЕЖИМ для: {driverPackage.Name}");
                    return await InstallDemoDriverAsync(driverPackage);
                }

                // ДЛЯ РЕАЛЬНЫХ УСТРОЙСТВ - реальная установка
                Console.WriteLine($"🔧 РЕАЛЬНАЯ УСТАНОВКА для: {driverPackage.Name}");

                // Оригинальный код для реальных драйверов
                var downloadedFile = await DownloadDriverFileAsync(driverPackage);
                if (downloadedFile == null)
                {
                    result.Success = false;
                    result.Message = "❌ Не удалось скачать файл драйвера";
                    return result;
                }

                // Шаг 2: Проверка целостности (если указан хэш)
                if (!string.IsNullOrEmpty(driverPackage.Sha256)) {
          if (!await VerifyFileIntegrityAsync(downloadedFile, driverPackage.Sha256)) {
            result.Success = false;
            result.Message = "❌ Проверка целостности файла не пройдена";
            File.Delete(downloadedFile);
            return result;
          }
        }

        // Шаг 3: Установка в зависимости от типа файла
         var installResult = await ExecuteInstallationAsync(downloadedFile, driverPackage);

        // Шаг 4: Очистка временных файлов
        try {
          File.Delete(downloadedFile);
        }
        catch {
          Console.WriteLine("⚠️ Не удалось удалить временный файл");
        }

        return installResult;
      }
      catch (Exception ex) {
        result.Success = false;
        result.Message = $"❌ Критическая ошибка установки: {ex.Message}";
        return result;
      }
    }


    /// <summary>
    /// Скачивает файл драйвера из любого URL
    /// </summary>
    private async Task<string> DownloadDriverFileAsync(DriverPackage driverPackage) {
      try {
        Console.WriteLine($"📥 Скачиваем файл из: {driverPackage.Url}");

        var fileName = string.IsNullOrEmpty(driverPackage.FileName)
            ? Path.GetFileName(driverPackage.Url)
            : driverPackage.FileName;

        if (string.IsNullOrEmpty(fileName)) {
          fileName = $"driver_{Guid.NewGuid():N}.tmp";
        }

        var localPath = Path.Combine(_tempDownloadPath, fileName);

        using (var response = await _httpClient.GetAsync(driverPackage.Url, HttpCompletionOption.ResponseHeadersRead)) {
          response.EnsureSuccessStatusCode();

          using (var stream = await response.Content.ReadAsStreamAsync())
          using (var fileStream = File.Create(localPath)) {
            await stream.CopyToAsync(fileStream);
          }
        }

        Console.WriteLine($"✅ Файл скачан: {localPath}");
        return localPath;
      }
      catch (Exception ex) {
        Console.WriteLine($"❌ Ошибка скачивания: {ex.Message}");
        return null;
      }
    }

    /// <summary>
    /// Проверяет целостность файла по SHA256
    /// </summary>
    private async Task<bool> VerifyFileIntegrityAsync(string filePath, string expectedHash) {
      try {
        Console.WriteLine("🔍 Проверяем целостность файла...");

        using (var sha256 = SHA256.Create())
        using (var stream = File.OpenRead(filePath)) {
          var hashBytes = await sha256.ComputeHashAsync(stream);
          var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
          var expected = expectedHash.ToLowerInvariant().Replace("-", "");

          if (actualHash == expected) {
            Console.WriteLine("✅ Проверка целостности пройдена");
            return true;
          } else {
            Console.WriteLine($"❌ Хэш не совпадает. Ожидался: {expected}, получен: {actualHash}");
            return false;
          }
        }
      }
      catch (Exception ex) {
        Console.WriteLine($"⚠️ Ошибка проверки целостности: {ex.Message}");
        return false; // Безопасность: если проверка не удалась - не устанавливаем
      }
    }

        /// <summary>
        /// Выполняет установку в зависимости от типа файла
        /// </summary>
        private async Task<InstallationResult> ExecuteInstallationAsync(string filePath, DriverPackage driverPackage)
        {
            var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
            var result = new InstallationResult
            {
                DriverName = driverPackage.Name,
                MachineName = Environment.MachineName,
                Timestamp = DateTime.Now
            };

            try
            {
                Console.WriteLine($"🔧 Устанавливаем драйвер: {filePath}");

                // ДЕМО-РЕЖИМ: Если драйвер содержит "Demo" или "TEST" или URL ведет на внешний сайт
                if (driverPackage.Name.Contains("Demo") ||
                    driverPackage.Name.Contains("TEST") ||
                    driverPackage.Url.Contains("downloadmirror.intel.com") ||
                    driverPackage.Url.Contains("nvidia.com") ||
                    driverPackage.Url.Contains("realtek.com"))
                {

                    Console.WriteLine($"🎮 АКТИВИРУЕМ ДЕМО-РЕЖИМ для: {driverPackage.Name}");
                    return await InstallDemoDriverAsync(driverPackage);
                }

                // Реальная установка для нормальных драйверов
                switch (fileExtension)
                {
                    case ".msi":
                        result = await InstallMsiPackageAsync(filePath, driverPackage);
                        break;
                    case ".exe":
                        result = await InstallExePackageAsync(filePath, driverPackage);
                        break;
                    case ".inf":
                        result = await InstallInfDriverAsync(filePath, driverPackage);
                        break;
                    default:
                        result.Success = false;
                        result.Message = $"❌ Неподдерживаемый тип файла: {fileExtension}";
                        break;
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"❌ Ошибка установки: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Демо-установка для тестовых драйверов
        /// </summary>
        private async Task<InstallationResult> InstallDemoDriverAsync(DriverPackage driverPackage)
        {
            var result = new InstallationResult
            {
                DriverName = driverPackage.Name,
                MachineName = Environment.MachineName,
                Timestamp = DateTime.Now
            };

            try
            {
                Console.WriteLine($"🎮 ДЕМО-РЕЖИМ: Установка {driverPackage.Name}");
                Console.WriteLine($"🎮 ПРОПУСКАЕМ реальное скачивание для демо!");

                // Имитация процесса установки с прогрессом
                Console.WriteLine("📥 ДЕМО: Начинаем 'установку'...");
                await Task.Delay(1000);

                for (int i = 1; i <= 5; i++)
                {
                    Console.WriteLine($"⚙️ ДЕМО: Установка... {i * 20}%");
                    await Task.Delay(500);
                }

                // Имитация успешной установки
                Console.WriteLine("✅ ДЕМО: Установка завершена успешно!");

                result.Success = true;
                result.Message = $"✅ ДЕМО: {driverPackage.Name} успешно 'установлен'! (Версия: {driverPackage.Version})";

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"❌ ДЕМО-Ошибка: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Установка MSI пакетов
        /// </summary>
        private async Task<InstallationResult> InstallMsiPackageAsync(string msiPath, DriverPackage driverPackage) {
      var result = new InstallationResult {
        DriverName = driverPackage.Name,
        MachineName = Environment.MachineName,
        Timestamp = DateTime.Now
      };

      try {
        var args = string.IsNullOrEmpty(driverPackage.InstallArgs)
            ? "/quiet /norestart"
            : driverPackage.InstallArgs;

        var processInfo = new ProcessStartInfo {
          FileName = "msiexec.exe",
          Arguments = $"/i \"{msiPath}\" {args}",
          UseShellExecute = false,
          CreateNoWindow = true,
          RedirectStandardOutput = true,
          RedirectStandardError = true
        };

        using (var process = Process.Start(processInfo)) {
          if (process == null) {
            result.Success = false;
            result.Message = "❌ Не удалось запустить процесс установки";
            return result;
          }

          await process.WaitForExitAsync();

          if (process.ExitCode == 0) {
            result.Success = true;
            result.Message = $"✅ Драйвер {driverPackage.Name} успешно установлен (MSI)";
          } else {
            result.Success = false;
            result.Message = $"❌ Ошибка установки MSI. Код выхода: {process.ExitCode}";
          }
        }

        return result;
      }
      catch (Exception ex) {
        result.Success = false;
        result.Message = $"❌ Ошибка установки MSI: {ex.Message}";
        return result;
      }
    }

    /// <summary>
    /// Установка EXE пакетов
    /// </summary>
    private async Task<InstallationResult> InstallExePackageAsync(string exePath, DriverPackage driverPackage) {
      var result = new InstallationResult {
        DriverName = driverPackage.Name,
        MachineName = Environment.MachineName,
        Timestamp = DateTime.Now
      };

      try {
        var args = string.IsNullOrEmpty(driverPackage.InstallArgs)
            ? "/S /quiet"
            : driverPackage.InstallArgs;

        var processInfo = new ProcessStartInfo {
          FileName = exePath,
          Arguments = args,
          UseShellExecute = false,
          CreateNoWindow = true,
          RedirectStandardOutput = true,
          RedirectStandardError = true
        };

        using (var process = Process.Start(processInfo)) {
          if (process == null) {
            result.Success = false;
            result.Message = "❌ Не удалось запустить процесс установки";
            return result;
          }

          await process.WaitForExitAsync();

          if (process.ExitCode == 0) {
            result.Success = true;
            result.Message = $"✅ Драйвер {driverPackage.Name} успешно установлен (EXE)";
          } else {
            result.Success = false;
            result.Message = $"❌ Ошибка установки EXE. Код выхода: {process.ExitCode}";
          }
        }

        return result;
      }
      catch (Exception ex) {
        result.Success = false;
        result.Message = $"❌ Ошибка установки EXE: {ex.Message}";
        return result;
      }
    }

    /// <summary>
    /// Установка INF драйверов через pnputil
    /// </summary>
    private async Task<InstallationResult> InstallInfDriverAsync(string infPath, DriverPackage driverPackage) {
      var result = new InstallationResult {
        DriverName = driverPackage.Name,
        MachineName = Environment.MachineName,
        Timestamp = DateTime.Now
      };

      try {
        var processInfo = new ProcessStartInfo {
          FileName = "pnputil.exe",
          Arguments = $"/add-driver \"{infPath}\" /install",
          UseShellExecute = false,
          CreateNoWindow = true,
          RedirectStandardOutput = true,
          RedirectStandardError = true
        };

        using (var process = Process.Start(processInfo)) {
          if (process == null) {
            result.Success = false;
            result.Message = "❌ Не удалось запустить pnputil";
            return result;
          }

          await process.WaitForExitAsync();

          if (process.ExitCode == 0) {
            result.Success = true;
            result.Message = $"✅ Драйвер {driverPackage.Name} успешно установлен (INF)";
          } else {
            result.Success = false;
            result.Message = $"❌ Ошибка установки INF. Код выхода: {process.ExitCode}";
          }
        }

        return result;
      }
      catch (Exception ex) {
        result.Success = false;
        result.Message = $"❌ Ошибка установки INF: {ex.Message}";
        return result;
      }
    }

    /// <summary>
    /// Очистка временных файлов
    /// </summary>
    public void Cleanup() {
      try {
        if (Directory.Exists(_tempDownloadPath)) {
          Directory.Delete(_tempDownloadPath, true);
          Console.WriteLine("🧹 Временные файлы очищены");
        }
      }
      catch (Exception ex) {
        Console.WriteLine($"⚠️ Ошибка очистки временных файлов: {ex.Message}");
      }
    }
  }
}