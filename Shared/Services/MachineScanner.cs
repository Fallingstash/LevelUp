using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace DriverDeploy.Shared.Services {
  public class MachineScanner {
    public static List<string> GetLocalIPRange() {
      // Получаем IP адрес текущей машины
      var localIP = Dns.GetHostAddresses(Dns.GetHostName())
          .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

      if (localIP == null) {
        return new List<string>();
      }

      // Генерируем диапазон IP (например, 192.168.1.1 - 192.168.1.255)
      var baseIP = localIP.ToString().Substring(0, localIP.ToString().LastIndexOf('.') + 1);
      return Enumerable.Range(1, 254).Select(i => $"{baseIP}{i}").ToList();
    }

    public static List<string> GetIPRangeWithLocalhost() {
      var ipRange = GetLocalIPRange();

      // Добавляем специальные адреса для тестирования на одном ПК
      ipRange.Add("127.0.0.1");    // localhost
      ipRange.Add("localhost");     // доменное имя

      // Также добавляем реальный IP текущей машины
      var localIP = Dns.GetHostAddresses(Dns.GetHostName())
          .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
      if (localIP != null) {
        ipRange.Add(localIP.ToString());
      }

      return ipRange.Distinct().ToList(); // Убираем дубликаты
    }

    public static async Task<bool> IsMachineOnline(string ip, int timeout = 1000) {
      try {
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(ip, timeout);
        return reply.Status == IPStatus.Success;
      }
      catch {
        return false;
      }
    }
  }
}
