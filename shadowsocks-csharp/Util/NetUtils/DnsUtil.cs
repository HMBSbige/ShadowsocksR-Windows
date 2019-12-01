using Shadowsocks.Controller;
using Shadowsocks.Model;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Shadowsocks.Util.NetUtils
{
    public static class DnsUtil
    {
        public static LRUCache<string, IPAddress> DnsBuffer { get; } = new LRUCache<string, IPAddress>();

        public static LRUCache<string, IPAddress> LocalDnsBuffer => DnsBuffer;

        public static IPAddress QueryDns(string host)
        {
            var res = host.Contains('.') && Global.GuiConfig.DnsClients.Any(s => s.Enable)
                    ? QueryAsync(host, Global.GuiConfig.DnsClients).Result
                    : QueryAsync(host).Result;
            Logging.Info(res == null
                    ? $@"DNS query {host} failed."
                    : $@"DNS query {host} answer {res}");
            return res;
        }

        public static async Task<IPAddress> QueryAsync(string host)
        {
            return await DnsClient.QueryIpAddressDefault(host, false, default);
        }

        public static async Task<IPAddress> QueryAsync(string host, IEnumerable<DnsClient> clients)
        {
            return await clients.Where(client => client.Enable)
                    .Select(s => Observable
                            .FromAsync(ct => s.QueryIpAddress(host, ct))
                            .Where(ip => ip != null)
                            .Select(ip => ip)
                    )
                    .Merge()
                    .FirstOrDefaultAsync();
        }
    }
}
