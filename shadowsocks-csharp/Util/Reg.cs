using System.Diagnostics;

namespace Shadowsocks.Util
{
    public static class Reg
    {
        [Conditional("RELEASE")]
        public static void SetUrlProtocol(string link)
        {
            try
            {
                var path = Utils.GetExecutablePath();
                using var runKey = Utils.OpenRegKey(@"Software\Classes", true);
                using var ssr = runKey?.CreateSubKey(link);
                if (ssr != null)
                {
                    ssr.SetValue(null, @"URL:ShadowsocksR Link");
                    ssr.SetValue(@"URL Protocol", @"");
                    using var command = ssr.CreateSubKey(@"Shell\Open\Command");
                    command?.SetValue(null, $@"""{path}"" ""%1""");
                }
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
                using var runKey = Utils.OpenRegKey(@"Software\Classes", true);
                runKey?.DeleteSubKeyTree(link);
            }
            catch
            {
                // ignored
            }
        }
    }
}
