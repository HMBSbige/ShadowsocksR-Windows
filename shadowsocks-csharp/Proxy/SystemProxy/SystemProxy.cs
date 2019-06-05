using Shadowsocks.Controller;
using Shadowsocks.Model;
using Shadowsocks.Util;
using System;
using System.IO;

namespace Shadowsocks.Proxy.SystemProxy
{
    public static class SystemProxy
    {
        private const string ProxyOverride = @"localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;172.32.*;192.168.*;<local>";

        private static void NotifyIE()
        {
            // These lines implement the Interface in the beginning of program 
            // They cause the OS to refresh the settings, causing IP to really update
            NativeMethods.InternetSetOption(IntPtr.Zero, (int)INTERNET_OPTION.INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            NativeMethods.InternetSetOption(IntPtr.Zero, (int)INTERNET_OPTION.INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }

        public static void Update(Configuration config, bool forceDisable, PACServer pacSrv)
        {
            var sysProxyMode = config.sysProxyMode;
            if (sysProxyMode == (int)ProxyMode.NoModify)
            {
                return;
            }
            if (forceDisable)
            {
                sysProxyMode = (int)ProxyMode.Direct;
            }
            var global = sysProxyMode == (int)ProxyMode.Global;
            var enabled = sysProxyMode != (int)ProxyMode.Direct;
            using var registry = Utils.OpenUserRegKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
            try
            {
                if (enabled)
                {
                    if (global)
                    {
                        Utils.RegistrySetValue(registry, @"ProxyEnable", 1);
                        Utils.RegistrySetValue(registry, @"ProxyServer", $@"127.0.0.1:{config.localPort}");
                        Utils.RegistrySetValue(registry, @"AutoConfigURL", string.Empty);
                    }
                    else
                    {
                        var pacUrl = pacSrv.PacUrl;
                        Utils.RegistrySetValue(registry, @"ProxyEnable", 0);
                        Utils.RegistrySetValue(registry, @"ProxyServer", string.Empty);
                        Utils.RegistrySetValue(registry, @"AutoConfigURL", pacUrl);
                    }
                }
                else
                {
                    Utils.RegistrySetValue(registry, @"ProxyEnable", 0);
                    Utils.RegistrySetValue(registry, @"ProxyServer", string.Empty);
                    Utils.RegistrySetValue(registry, @"AutoConfigURL", string.Empty);
                }

                IEProxyUpdate(config, sysProxyMode, pacSrv == null ? string.Empty : pacSrv.PacUrl);
                NotifyIE();
                //Must Notify IE first, or the connections do not change
                CopyProxySettingFromLan();
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
            }
        }

        private static void CopyProxySettingFromLan()
        {
            using var registry = Utils.OpenUserRegKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Connections", true);
            try
            {
                var defaultValue = registry.GetValue(@"DefaultConnectionSettings");
                var connections = registry.GetValueNames();
                foreach (var each in connections)
                {
                    switch (each.ToUpperInvariant())
                    {
                        case @"DEFAULTCONNECTIONSETTINGS":
                        case @"SAVEDLEGACYSETTINGS":
                            //case "LAN CONNECTION":
                            continue;
                        default:
                            //set all the connections's proxy as the lan
                            registry.SetValue(each, defaultValue);
                            continue;
                    }
                }
                NotifyIE();
            }
            catch (IOException e)
            {
                Logging.LogUsefulException(e);
            }
        }

        private static void BytePushBack(byte[] buffer, ref int buffer_len, int val)
        {
            BitConverter.GetBytes(val).CopyTo(buffer, buffer_len);
            buffer_len += 4;
        }

        private static void BytePushBack(byte[] buffer, ref int buffer_len, string str)
        {
            BytePushBack(buffer, ref buffer_len, str.Length);
            var bytes = System.Text.Encoding.UTF8.GetBytes(str);
            bytes.CopyTo(buffer, buffer_len);
            buffer_len += bytes.Length;
        }

        private static byte[] GenConnectionSettings(Configuration config, int sysProxyMode, int counter, string pacUrl)
        {
            var buffer = new byte[1024];
            var buffer_len = 0;
            BytePushBack(buffer, ref buffer_len, 70);
            BytePushBack(buffer, ref buffer_len, counter + 1);
            if (sysProxyMode == (int)ProxyMode.Direct)
                BytePushBack(buffer, ref buffer_len, 1);
            else if (sysProxyMode == (int)ProxyMode.Pac)
                BytePushBack(buffer, ref buffer_len, 5);
            else
                BytePushBack(buffer, ref buffer_len, 3);

            var proxy = $@"127.0.0.1:{config.localPort}";
            BytePushBack(buffer, ref buffer_len, proxy);

            var bypass = sysProxyMode == (int)ProxyMode.Global ? string.Empty : ProxyOverride;
            BytePushBack(buffer, ref buffer_len, bypass);

            BytePushBack(buffer, ref buffer_len, pacUrl);

            buffer_len += 0x20;

            Array.Resize(ref buffer, buffer_len);
            return buffer;
        }

        /// <summary>
        /// Checks or unchecks the IE Options Connection setting of "Automatically detect Proxy"
        /// </summary>
        private static void IEProxyUpdate(Configuration config, int sysProxyMode, string pacUrl)
        {
            using (var registry = Utils.OpenUserRegKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Connections", true))
            {
                try
                {
                    var defConnection = (byte[])registry.GetValue(@"DefaultConnectionSettings");
                    var counter = 0;
                    if (defConnection != null && defConnection.Length >= 8)
                    {
                        counter = defConnection[4] | (defConnection[5] << 8);
                    }
                    defConnection = GenConnectionSettings(config, sysProxyMode, counter, pacUrl);
                    Utils.RegistrySetValue(registry, @"DefaultConnectionSettings", defConnection);
                    Utils.RegistrySetValue(registry, @"SavedLegacySettings", defConnection);
                }
                catch (IOException e)
                {
                    Logging.LogUsefulException(e);
                }
            }
            using var registry2 = Utils.OpenUserRegKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
            try
            {
                Utils.RegistrySetValue(registry2, @"ProxyOverride", ProxyOverride);
            }
            catch (IOException e)
            {
                Logging.LogUsefulException(e);
            }
        }
    }
}
