using System.Net;

namespace Shadowsocks.Model
{
    public static class GlobalConfiguration
    {
        public static bool OSSupportsLocalIPv6 = false;

        public static string LocalHost => OSSupportsLocalIPv6 ? $@"[{IPAddress.IPv6Loopback}]" : $@"{IPAddress.Loopback}";

        public static string AnyHost => OSSupportsLocalIPv6 ? $@"[{IPAddress.IPv6Any}]" : $@"{IPAddress.Any}";

        public static Configuration GuiConfig;
    }
}