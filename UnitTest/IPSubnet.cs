using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shadowsocks.Util.NetUtils;
using System.Net;

namespace UnitTest
{
    [TestClass]
    public class IPSubnetTest
    {
        [TestMethod]
        public void IsLoopBackTest()
        {
            Assert.IsTrue(IPSubnet.IsLoopBack(IPAddress.Loopback));
            Assert.IsTrue(IPSubnet.IsLoopBack(IPAddress.IPv6Loopback));
            Assert.IsTrue(IPSubnet.IsLoopBack(IPAddress.Parse(@"127.0.0.255")));
            Assert.IsTrue(IPSubnet.IsLoopBack(IPAddress.Parse(@"127.255.255.255")));
            Assert.IsFalse(IPSubnet.IsLoopBack(IPAddress.Parse(@"192.168.1.1")));
            Assert.IsFalse(IPSubnet.IsLoopBack(IPAddress.Parse(@"2001:0DB8:ABCD:0012:FFFF:FFFF:FFFF:FFFF")));
        }

        [TestMethod]
        public void IsInSubnetTest()
        {
            Assert.IsTrue(IPAddress.Parse(@"192.168.5.1").IsInSubnet(@"192.168.5.85/24"));
            Assert.IsTrue(IPAddress.Parse(@"192.168.5.254").IsInSubnet(@"192.168.5.85/24"));
            Assert.IsTrue(IPAddress.Parse(@"10.128.240.48").IsInSubnet(@"10.128.240.50/30"));
            Assert.IsTrue(IPAddress.Parse(@"10.128.240.49").IsInSubnet(@"10.128.240.50/30"));
            Assert.IsTrue(IPAddress.Parse(@"10.128.240.50").IsInSubnet(@"10.128.240.50/30"));
            Assert.IsTrue(IPAddress.Parse(@"10.128.240.51").IsInSubnet(@"10.128.240.50/30"));

            Assert.IsFalse(IPAddress.Parse(@"192.168.4.254").IsInSubnet(@"192.168.5.85/24"));
            Assert.IsFalse(IPAddress.Parse(@"191.168.5.254").IsInSubnet(@"192.168.5.85/24"));
            Assert.IsFalse(IPAddress.Parse(@"10.128.240.47").IsInSubnet(@"10.128.240.50/30"));
            Assert.IsFalse(IPAddress.Parse(@"10.128.240.52").IsInSubnet(@"10.128.240.50/30"));
            Assert.IsFalse(IPAddress.Parse(@"10.128.239.50").IsInSubnet(@"10.128.240.50/30"));
            Assert.IsFalse(IPAddress.Parse(@"10.127.240.51").IsInSubnet(@"10.128.240.50/30"));

            Assert.IsTrue(IPAddress.Parse(@"2001:0DB8:ABCD:0012:0000:0000:0000:0000").IsInSubnet(@"2001:db8:abcd:0012::0/64"));
            Assert.IsTrue(IPAddress.Parse(@"2001:0DB8:ABCD:0012:FFFF:FFFF:FFFF:FFFF").IsInSubnet(@"2001:db8:abcd:0012::0/64"));
            Assert.IsTrue(IPAddress.Parse(@"2001:0DB8:ABCD:0012:0001:0000:0000:0000").IsInSubnet(@"2001:db8:abcd:0012::0/64"));
            Assert.IsTrue(IPAddress.Parse(@"2001:0DB8:ABCD:0012:FFFF:FFFF:FFFF:FFF0").IsInSubnet(@"2001:db8:abcd:0012::0/64"));
            Assert.IsTrue(IPAddress.Parse(@"2001:0DB8:ABCD:0012:0000:0000:0000:0000").IsInSubnet(@"2001:db8:abcd:0012::0/128"));

            Assert.IsFalse(IPAddress.Parse(@"2001:0DB8:ABCD:0011:FFFF:FFFF:FFFF:FFFF").IsInSubnet(@"2001:db8:abcd:0012::0/64"));
            Assert.IsFalse(IPAddress.Parse(@"2001:0DB8:ABCD:0013:0000:0000:0000:0000").IsInSubnet(@"2001:db8:abcd:0012::0/64"));
            Assert.IsFalse(IPAddress.Parse(@"2001:0DB8:ABCD:0013:0001:0000:0000:0000").IsInSubnet(@"2001:db8:abcd:0012::0/64"));
            Assert.IsFalse(IPAddress.Parse(@"2001:0DB8:ABCD:0011:FFFF:FFFF:FFFF:FFF0").IsInSubnet(@"2001:db8:abcd:0012::0/64"));
            Assert.IsFalse(IPAddress.Parse(@"2001:0DB8:ABCD:0012:0000:0000:0000:0001").IsInSubnet(@"2001:db8:abcd:0012::0/128"));
        }
    }
}
