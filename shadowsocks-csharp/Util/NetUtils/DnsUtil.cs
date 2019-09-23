using DnsClient;
using DnsClient.Protocol;
using Shadowsocks.Controller;
using Shadowsocks.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Shadowsocks.Util.NetUtils
{
    public static class DnsUtil
    {
        public static LRUCache<string, IPAddress> DnsBuffer { get; } = new LRUCache<string, IPAddress>();

        public static LRUCache<string, IPAddress> LocalDnsBuffer => DnsBuffer;

        private static IPEndPoint[] ToIpEndPoints(string dnsServers, ushort defaultPort = 53)
        {
            var dnsServerStr = dnsServers.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
            var dnsServer = new List<IPEndPoint>();
            foreach (var serverStr in dnsServerStr)
            {
                var server = serverStr.Trim();
                var index = server.IndexOf(':');
                string ip = null;
                string port = null;
                if (index >= 0)
                {
                    if (server.StartsWith("["))
                    {
                        var ipv6_end = server.IndexOf(']', 1);
                        if (ipv6_end >= 0)
                        {
                            ip = server.Substring(1, ipv6_end - 1);

                            index = server.IndexOf(':', ipv6_end);
                            if (index == ipv6_end + 1)
                            {
                                port = server.Substring(index + 1);
                            }
                        }
                    }
                    else
                    {
                        ip = server.Substring(0, index);
                        port = server.Substring(index + 1);
                    }
                }
                else
                {
                    index = server.IndexOf(' ');
                    if (index >= 0)
                    {
                        ip = server.Substring(0, index);
                        port = server.Substring(index + 1);
                    }
                    else
                    {
                        ip = server;
                    }
                }

                if (ip != null && IPAddress.TryParse(ip, out var ipAddress))
                {
                    var iPort = defaultPort;
                    if (port != null)
                    {
                        ushort.TryParse(port, out iPort);
                    }

                    dnsServer.Add(new IPEndPoint(ipAddress, iPort));
                }
            }

            return dnsServer.ToArray();
        }

        public static IPAddress QueryDns(string host, string dns_servers, bool IPv6_first = false)
        {
            var retIpAddress = Query(host, dns_servers, IPv6_first);
            Logging.Info(retIpAddress == null
                    ? $@"DNS query {host} failed."
                    : $@"DNS query {host} answer {retIpAddress}");

            return retIpAddress;
        }

        private static IPAddress Query(string host, string dnsServers, bool ipv6_first = false)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(dnsServers))
                {
                    var client = new LookupClient(ToIpEndPoints(dnsServers))
                    {
                        UseCache = false
                    };
                    IPAddress r;
                    if (ipv6_first)
                    {
                        try
                        {
                            r = client.Query(host, QueryType.AAAA).Answers.OfType<AaaaRecord>().FirstOrDefault()
                                    ?.Address;
                        }
                        catch (DnsResponseException)
                        {
                            client.UseTcpOnly = true;
                            r = client.Query(host, QueryType.AAAA).Answers.OfType<AaaaRecord>().FirstOrDefault()
                                    ?.Address;
                        }

                        if (r != null)
                        {
                            return r;
                        }

                        r = client.Query(host, QueryType.A).Answers.OfType<ARecord>().FirstOrDefault()?.Address;
                        if (r != null)
                        {
                            return r;
                        }
                    }
                    else
                    {
                        try
                        {
                            r = client.Query(host, QueryType.A).Answers.OfType<ARecord>().FirstOrDefault()?.Address;
                        }
                        catch (DnsResponseException)
                        {
                            client.UseTcpOnly = true;
                            r = client.Query(host, QueryType.A).Answers.OfType<ARecord>().FirstOrDefault()?.Address;
                        }

                        if (r != null)
                        {
                            return r;
                        }

                        r = client.Query(host, QueryType.AAAA).Answers.OfType<AaaaRecord>().FirstOrDefault()?.Address;
                        if (r != null)
                        {
                            return r;
                        }
                    }
                }
                else
                {
                    var ips = Dns.GetHostAddresses(host);
                    var type = ipv6_first ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;

                    foreach (var ad in ips)
                    {
                        if (ad.AddressFamily == type)
                        {
                            return ad;
                        }
                    }

                    foreach (var ad in ips)
                    {
                        return ad;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }
    }
}
