using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Security.Authentication;

namespace UnitTest
{
    [TestClass]
    public class DnsTest
    {
        [TestMethod]
        public void Test()
        {
            var ip = IPAddress.Parse(@"101.6.6.6");
            const int port = 5353;
            var domain = DomainName.Parse(@"www.google.com");
            var options = new DnsQueryOptions
            {
                IsEDnsEnabled = true,
                IsRecursionDesired = true,
                EDnsOptions = new OptRecord { Options = { new ClientSubnetOption(32, ip) } }
            };

            var dnsClient = new ARSoft.Tools.Net.Dns.DnsClient(ip, 10000, port)
            {
                IsTcpEnabled = true,
                IsUdpEnabled = false
            };

            var message = dnsClient.Resolve(domain, RecordType.A, RecordClass.INet, options);
            Assert.IsTrue(message.AnswerRecords.Count > 0);
            foreach (var answerRecord in message.AnswerRecords)
            {
                Console.WriteLine(answerRecord);
            }
        }

        [TestMethod]
        public void DnsOverTlsTest()
        {
            var ip = IPAddress.Parse(@"8.8.8.8");
            var tlsServer = new TlsUpstreamServer
            {
                IPAddress = ip,
                AuthName = @"dns.google",
                SslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12
            };
            var domain = DomainName.Parse(@"www.google.com");
            var options = new DnsQueryOptions
            {
                IsEDnsEnabled = true,
                IsRecursionDesired = true,
                EDnsOptions = new OptRecord { Options = { new ClientSubnetOption(32, ip) } }
            };

            var dnsClient = new DnsOverTlsClient(tlsServer, 10000);

            var message = dnsClient.Resolve(domain, RecordType.A, RecordClass.INet, options);
            Assert.IsTrue(message.AnswerRecords.Count > 0);
            foreach (var answerRecord in message.AnswerRecords)
            {
                Console.WriteLine(answerRecord);
            }
        }
    }
}
