using System.Diagnostics;
using URIScheme;

namespace Shadowsocks.Util
{
    public static class Reg
    {
        [Conditional("RELEASE")]
        public static void SetUrlProtocol(string link)
        {
            try
            {
                var service = new URISchemeService(link, @"URL:ShadowsocksR Link", Utils.GetExecutablePath());
                service.Set();
            }
            catch
            {
                // ignored
            }
        }

        [Conditional("RELEASE")]
        public static void RemoveUrlProtocol(string link)
        {
            try
            {
                var service = new URISchemeService(link, string.Empty, string.Empty);
                service.Delete();
            }
            catch
            {
                // ignored
            }
        }
    }
}
