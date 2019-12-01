using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shadowsocks.Enums;
using Shadowsocks.Util.NetUtils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace UnitTest
{
    [TestClass]
    public class DnsTest
    {
        [TestMethod]
        public async Task DefaultTest()
        {
            var ip1 = await DnsUtil.QueryAsync(@"dns.google");
            Assert.IsTrue(Equals(ip1, IPAddress.Parse(@"8.8.8.8")) || Equals(ip1, IPAddress.Parse(@"8.8.4.4")));
            var ip2 = await DnsUtil.QueryAsync(@"dns.google");
            Assert.IsTrue(Equals(ip2, IPAddress.Parse(@"2001:4860:4860::8888")) || Equals(ip2, IPAddress.Parse(@"2001:4860:4860::8844")));
        }

        [TestMethod]
        public async Task Test()
        {
            var client = new Shadowsocks.Model.DnsClient(DnsType.Default)
            {
                DnsServer = @"101.6.6.6",
                Port = 5353,
                IsTcpEnabled = true,
                IsUdpEnabled = false
            };
            var ip = await client.QueryIpAddress(@"www.google.com", default);
            Assert.IsNotNull(ip);
            Console.WriteLine(ip);
        }

        [TestMethod]
        public async Task DnsOverTlsTest()
        {
            var client = new Shadowsocks.Model.DnsClient(DnsType.DnsOverTls);
            var ip = await client.QueryIpAddress(@"www.google.com", default);
            Assert.IsNotNull(ip);
            Console.WriteLine(ip);
        }

        [TestMethod]
        public async Task TestTwoDnsAsync()
        {
            const string host = @"www.google.com";
            var clients = new List<Shadowsocks.Model.DnsClient>
            {
                new Shadowsocks.Model.DnsClient(DnsType.Default)
                {
                        DnsServer = @"101.6.6.6",
                        Port = 5353,
                        IsTcpEnabled = true,
                        IsUdpEnabled = false
                },
                new Shadowsocks.Model.DnsClient(DnsType.DnsOverTls)
            };
            var res = await DnsUtil.QueryAsync(host, clients);
            Assert.IsNotNull(res);
            Console.WriteLine(res);
        }
    }
}
