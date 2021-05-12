using CryptoBase;
using RunAtStartup;
using System;
using System.IO;
using Utils = Shadowsocks.Util.Utils;

namespace Shadowsocks.Controller
{
    internal static class AutoStartup
    {
        private static readonly string Key = $@"ShadowsocksR_{Directory.GetCurrentDirectory().GetClassicHashCode()}";

        public static bool Set(bool enabled)
        {
            try
            {
                var path = $@"""{Utils.GetExecutablePath()}""";
                var service = new StartupService(Key);
                if (enabled)
                {
                    service.Set(path);
                }
                else
                {
                    service.Delete();
                }
                return true;
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                return false;
            }
        }

        public static bool Check()
        {
            try
            {
                var path = $@"""{Utils.GetExecutablePath()}""";
                var service = new StartupService(Key);
                return service.Check(path);
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                return false;
            }
        }
    }
}
