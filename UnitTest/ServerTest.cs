using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shadowsocks.Model;
using System;

namespace UnitTest
{
    [TestClass]
    public class ServerTest
    {
        [TestMethod]
        public void TestServerFromSSR()
        {
            var server = new Server();
            var nornameCase = "ssr://MTI3LjAuMC4xOjEyMzQ6YXV0aF9hZXMxMjhfbWQ1OmFlcy0xMjgtY2ZiOnRsczEuMl90aWNrZXRfYXV0aDpZV0ZoWW1KaS8_b2Jmc3BhcmFtPVluSmxZV3QzWVRFeExtMXZaUQ";

            server.ServerFromSsr(nornameCase, "");

            Assert.AreEqual(server.server, "127.0.0.1");
            Assert.AreEqual(server.Server_Port, (ushort)1234);
            Assert.AreEqual(server.Protocol, "auth_aes128_md5");
            Assert.AreEqual(server.Method, "aes-128-cfb");
            Assert.AreEqual(server.obfs, "tls1.2_ticket_auth");
            Assert.AreEqual(server.ObfsParam, "breakwa11.moe");
            Assert.AreEqual(server.Password, "aaabbb");

            server = new Server();
            const string normalCaseWithRemark = "ssr://MTI3LjAuMC4xOjEyMzQ6YXV0aF9hZXMxMjhfbWQ1OmFlcy0xMjgtY2ZiOnRsczEuMl90aWNrZXRfYXV0aDpZV0ZoWW1KaS8_b2Jmc3BhcmFtPVluSmxZV3QzWVRFeExtMXZaUSZyZW1hcmtzPTVyV0w2Sy1WNUxpdDVwYUg";

            server.ServerFromSsr(normalCaseWithRemark, "firewallAirport");

            Assert.AreEqual(server.server, "127.0.0.1");
            Assert.AreEqual<ushort>(server.Server_Port, 1234);
            Assert.AreEqual(server.Protocol, "auth_aes128_md5");
            Assert.AreEqual(server.Method, "aes-128-cfb");
            Assert.AreEqual(server.obfs, "tls1.2_ticket_auth");
            Assert.AreEqual(server.ObfsParam, "breakwa11.moe");
            Assert.AreEqual(server.Password, "aaabbb");

            Assert.AreEqual(server.Remarks, "测试中文");
            Assert.AreEqual(server.Group, string.Empty);
            Assert.AreEqual(server.SubTag, "firewallAirport");
        }

        [TestMethod]
        public void TestBadPortNumber()
        {
            var server = new Server();

            const string link = "ssr://MTI3LjAuMC4xOjgwOmF1dGhfc2hhMV92NDpjaGFjaGEyMDpodHRwX3NpbXBsZTplaWZnYmVpd3ViZ3IvP29iZnNwYXJhbT0mcHJvdG9wYXJhbT0mcmVtYXJrcz0mZ3JvdXA9JnVkcHBvcnQ9NDY0MzgxMzYmdW90PTQ2MDA3MTI4";
            try
            {
                server.ServerFromSsr(link, "");
            }
            catch (OverflowException e)
            {
                Console.Write(e.ToString());
            }

        }
    }
}
