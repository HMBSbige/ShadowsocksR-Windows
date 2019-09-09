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
            if (Equals(ip, IPAddress.IPv6Loopback))
            {
                return true;
            }
            return ip.IsInSubnet(@"127.0.0.0/8");
        }
    }
}
