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
      var localIP = Dns.GetHostAddresses(Dns.GetHostName())
          .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

      if (localIP == null) {
        return new List<string>();
      }

      var baseIP = localIP.ToString().Substring(0, localIP.ToString().LastIndexOf('.') + 1);
      return Enumerable.Range(1, 254).Select(i => $"{baseIP}{i}").ToList();
    }

    public static List<string> GetIPRangeWithLocalhost() {
      var ipRange = GetLocalIPRange();

      ipRange.Add("127.0.0.1");    
      ipRange.Add("localhost");     

      var localIP = Dns.GetHostAddresses(Dns.GetHostName())
          .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
      if (localIP != null) {
        ipRange.Add(localIP.ToString());
      }

      return ipRange.Distinct().ToList(); 
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
