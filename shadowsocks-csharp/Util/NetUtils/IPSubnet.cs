using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Shadowsocks.Util.NetUtils
{
    public static class IPSubnet
    {
        public static bool IsInSubnet(this IPAddress address, string subnetMask)
        {
            var slashIdx = subnetMask.IndexOf("/", StringComparison.Ordinal);
            if (!subnetMask.Contains("/"))
            {
                // We only handle netmasks in format "IP/PrefixLength".
                throw new NotSupportedException("Only SubNetMasks with a given prefix length are supported.");
            }

            // First parse the address of the netmask before the prefix length.
            var maskAddress = IPAddress.Parse(subnetMask.Substring(0, slashIdx));

            if (maskAddress.AddressFamily != address.AddressFamily)
            { // We got something like an IPV4-Address for an IPv6-Mask. This is not valid.
                return false;
            }

            // Now find out how long the prefix is.
            var maskLength = int.Parse(subnetMask.Substring(slashIdx + 1));

            if (maskAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                // Convert the mask address to an unsigned integer.
                var maskAddressBits = BitConverter.ToUInt32(maskAddress.GetAddressBytes().Reverse().ToArray());

                // And convert the IpAddress to an unsigned integer.
                var ipAddressBits = BitConverter.ToUInt32(address.GetAddressBytes().Reverse().ToArray());

                // Get the mask/network address as unsigned integer.
                var mask = uint.MaxValue << (32 - maskLength);

                // https://stackoverflow.com/a/1499284/3085985
                // Bitwise AND mask and MaskAddress, this should be the same as mask and IpAddress
                // as the end of the mask is 0000 which leads to both addresses to end with 0000
                // and to start with the prefix.
                return (maskAddressBits & mask) == (ipAddressBits & mask);
            }

            if (maskAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // Convert the mask address to a BitArray.
                var maskAddressBits = new BitArray(maskAddress.GetAddressBytes());

                // And convert the IpAddress to a BitArray.
                var ipAddressBits = new BitArray(address.GetAddressBytes());

                if (maskAddressBits.Length != ipAddressBits.Length)
                {
                    throw new ArgumentException("Length of IP Address and Subnet Mask do not match.");
                }

                // Compare the prefix bits.
                for (var i = 0; i < maskLength; i++)
                {
                    if (ipAddressBits[i] != maskAddressBits[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            throw new NotSupportedException("Only InterNetworkV6 or InterNetwork address families are supported.");
        }

        public static bool IsLoopBack(IPAddress ip)
        {
            return Equals(ip, IPAddress.IPv6Loopback) || ip.IsInSubnet(@"127.0.0.0/8");
        }

        public static bool IsLocal(IPAddress ip)
        {
            return ip.IsInSubnet(@"127.0.0.0/8") || ip.IsInSubnet(@"169.254.0.0/16") || ip.IsInSubnet(@"::1/128");
        }

        public static bool IsLocal(Socket socket)
        {
            return IsLocal(((IPEndPoint)socket.RemoteEndPoint).Address);
        }

        public static bool IsLan(IPAddress ip)
        {
            var netmasks = new[]
            {
                    @"0.0.0.0/8",
                    @"10.0.0.0/8",
                    //"100.64.0.0/10", //部分地区运营商貌似在使用这个，这个可能不安全
                    @"127.0.0.0/8",
                    @"169.254.0.0/16",
                    @"172.16.0.0/12",
                    //"192.0.0.0/24",
                    //"192.0.2.0/24",
                    //"192.88.99.0/24",
                    @"192.168.0.0/16",
                    //"198.18.0.0/15",
                    //"198.51.100.0/24",
                    //"203.0.113.0/24",
                    @"::1/128",
                    @"fc00::/7",
                    @"fe80::/10"
            };
            return netmasks.Any(ip.IsInSubnet);
        }

        public static bool IsLan(Socket socket)
        {
            return IsLan(((IPEndPoint)socket.RemoteEndPoint).Address);
        }
    }
}
