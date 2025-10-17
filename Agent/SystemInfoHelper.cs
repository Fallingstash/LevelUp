using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Security.Principal;

namespace DriverDeploy.Agent.Services {
  public static class SystemInfoHelper {
    public static bool IsRunningAsAdministrator() {
      try {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
      }
      catch {
        return false;
      }
    }

    public static string GetSystemArchitecture() {
      return Environment.Is64BitOperatingSystem ? "x64" : "x86";
    }

    public static (long TotalGB, long FreeGB) GetDiskSpaceInfo() {
      try {
        var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
        return (
            drive.TotalSize / (1024 * 1024 * 1024),
            drive.AvailableFreeSpace / (1024 * 1024 * 1024)
        );
      }
      catch {
        return (0, 0);
      }
    }

    public static void LogSystemInfo() {
      Console.WriteLine($"💻 Информация о системе:");
      Console.WriteLine($"   Имя машины: {Environment.MachineName}");
      Console.WriteLine($"   ОС: {Environment.OSVersion.VersionString}");
      Console.WriteLine($"   Архитектура: {GetSystemArchitecture()}");
      Console.WriteLine($"   Администратор: {(IsRunningAsAdministrator() ? "Да" : "Нет")}");

      var (total, free) = GetDiskSpaceInfo();
      Console.WriteLine($"   Диск: {free}GB свободно из {total}GB");
    }
  }
}