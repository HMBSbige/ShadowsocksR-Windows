using Shadowsocks.Controller;
using Shadowsocks.Model;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading.Tasks;

#nullable enable

namespace Shadowsocks.Util.NetUtils
{
    public static class DnsUtil
    {
        public static LRUCache<string, IPAddress> DnsBuffer { get; } = new();

        public static IPAddress? QueryDns(string host)
        {
            var res = host.Contains('.') && Global.GuiConfig.DnsClients.Any(s => s.Enable)
                    ? QueryAsync(host, Global.GuiConfig.DnsClients).Result
                    : QueryDefaultAsync(host).Result;
            Logging.Info(res is null
                    ? $@"DNS query {host} failed."
                    : $@"DNS query {host} answer {res}");
            return res;
        }

        public static async Task<IPAddress?> QueryDefaultAsync(string host, bool ipv6First = default)
        {
            return await DnsClient.QueryIpAddressDefaultAsync(host, ipv6First, default);
        }

        public static async Task<IPAddress?> QueryAsync(string host, IEnumerable<DnsClient> clients)
        {
            return await clients
                    .Where(client => client.Enable)
                    .Select(s => Observable
                            .FromAsync(ct => s.QueryIpAddressAsync(host, ct))
                            .Where(ip => ip is not null)
                    )
                    .Merge()
                    .FirstOrDefaultAsync();
        }
    }
}
