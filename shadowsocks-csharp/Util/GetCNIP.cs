using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Shadowsocks.Util
{
    internal class IPv4Subnet
    {
        #region data
        public readonly IPAddress Netmask;
        public readonly IPAddress Wildcard;
        public readonly int CIDR;
        public readonly int Hosts;
        public readonly IPAddress FirstIP;
        public readonly IPAddress LastIP;
        #endregion

        private static uint IPv42UintLE(IPAddress ipv4)
        {
            var buf = ipv4.GetAddressBytes();
            return BitConverter.ToUInt32(buf, 0);
        }

        public static uint IPv42UintBE(IPAddress ipv4)
        {
            var buf = ipv4.GetAddressBytes();
            Array.Reverse(buf);
            return BitConverter.ToUInt32(buf, 0);
        }

        private static IPAddress IPv4BinStrToIPv4(string str)
        {
            var bytesAddress = new[]
            {
                    Convert.ToByte(str.Substring(0, 8),2),
                    Convert.ToByte(str.Substring(8, 8),2),
                    Convert.ToByte(str.Substring(16, 8),2),
                    Convert.ToByte(str.Substring(24, 8),2)
            };
            return new IPAddress(bytesAddress);
        }

        private static string IPv4ToIPv4BinStr(IPAddress ipv4)
        {
            var bytesAddress = ipv4.GetAddressBytes();

            return $@"{Convert.ToString(bytesAddress[0], 2).PadLeft(8, '0')}{
                       Convert.ToString(bytesAddress[1], 2).PadLeft(8, '0')}{
                       Convert.ToString(bytesAddress[2], 2).PadLeft(8, '0')}{
                       Convert.ToString(bytesAddress[3], 2).PadLeft(8, '0')}";
        }

        public static int Hosts2CIDR(int hosts)
        {
            return 32 - Convert.ToInt32(Math.Log(hosts, 2));
        }

        public static int CIDR2Hosts(int CIDR)
        {
            return Convert.ToInt32(Math.Pow(2, 32 - Convert.ToInt32(CIDR)));
        }

        public IPv4Subnet(IPAddress ipv4, int hosts)
        {
            Hosts = hosts;
            CIDR = Hosts2CIDR(hosts);

            var netmaskStr = new string('1', CIDR) + new string('0', 32 - CIDR);
            Netmask = IPv4BinStrToIPv4(netmaskStr);
            var netmaskUint = IPv42UintLE(Netmask);

            var wildcardStr = new string('0', CIDR) + new string('1', 32 - CIDR);
            Wildcard = IPv4BinStrToIPv4(wildcardStr);
            var wildcardUint = IPv42UintLE(Wildcard);

            var ipv4Uint = IPv42UintLE(ipv4);

            FirstIP = new IPAddress(ipv4Uint & netmaskUint);
            LastIP = new IPAddress(ipv4Uint | wildcardUint);
        }
    }

    internal static class GetCNIP
    {
        #region GetStrings

        public static string GetcnIpRange(Dictionary<IPAddress, int> ipv4Subnets)
        {
            var sb = new StringBuilder("[\n{");

            uint startNum = 0;
            var comma = @"";
            foreach (var ipv4Subnet in ipv4Subnets)
            {
                var p = new IPv4Subnet(ipv4Subnet.Key, ipv4Subnet.Value);

                while (IPv4Subnet.IPv42UintBE(p.FirstIP) >> 24 > startNum)
                {
                    ++startNum;
                    sb.Append(@"},{");
                    comma = @"";
                }

                sb.Append(comma);
                sb.Append(@"0x");
                sb.Append(Convert.ToString(IPv4Subnet.IPv42UintBE(p.FirstIP) / 256, 16));
                sb.Append(@":");
                sb.Append(ipv4Subnet.Value / 256);
                comma = @",";
            }
            sb.Append("}\n];");
            return sb.ToString();
        }

        public static string GetcnIp16Range(Dictionary<IPAddress, int> ipv4Subnets)
        {
            var sb = new StringBuilder("{\n");

            var masterNetSet = new SortedSet<uint>();
            foreach (var ipv4Subnet in ipv4Subnets)
            {
                var p = new IPv4Subnet(ipv4Subnet.Key, ipv4Subnet.Value);
                if (ipv4Subnet.Value < 1 << 14)
                {
                    masterNetSet.Add(IPv4Subnet.IPv42UintBE(p.FirstIP) >> 14);
                }
            }
            var masterNet = new List<uint>(masterNetSet.Count);
            foreach (var x in masterNetSet)
            {
                masterNet.Add(x);
            }
            //masterNet.Sort();
            foreach (var x in masterNet)
            {
                sb.Append(@"0x");
                sb.Append(Convert.ToString(x, 16));
                sb.Append(@":1,");
            }
            sb.Remove(sb.Length - 1, 1);
            sb.Append("\n};");
            return sb.ToString();
        }

        #endregion

        private static KeyValuePair<IPAddress, int>? GetCNIPv4InfoFromLine(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return null;
            }

            var strA = str.Split('|');
            //apnic|CN|ipv4|
            if (strA.Length > 4 && strA[0] == @"apnic" && strA[1] == @"CN" && strA[2] == @"ipv4")
            {
                return new KeyValuePair<IPAddress, int>(IPAddress.Parse(strA[3]), Convert.ToInt32(strA[4]));
            }

            return null;
        }

        public static Dictionary<IPAddress, int> ReadFromString(string str)
        {
            var ipv4Subnet = new Dictionary<IPAddress, int>();
            var lines = str.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var p = GetCNIPv4InfoFromLine(line);
                if (p != null)
                {
                    ipv4Subnet.Add(p.Value.Key, p.Value.Value);
                }
            }
            return ipv4Subnet.Count == 0 ? null : ipv4Subnet;
        }
    }
}
