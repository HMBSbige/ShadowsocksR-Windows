using Shadowsocks.Controller;
using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Controller.Service;
using Shadowsocks.Util;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Shadowsocks.Model
{
    public static class Global
    {
        private const string ConfigFile = @"gui-config.json";

        public static bool OSSupportsLocalIPv6 => Socket.OSSupportsIPv6;

        public static string LocalHost => OSSupportsLocalIPv6 ? $@"[{IPAddress.IPv6Loopback}]" : $@"{IPAddress.Loopback}";

        public static string AnyHost => OSSupportsLocalIPv6 ? $@"[{IPAddress.IPv6Any}]" : $@"{IPAddress.Any}";

        public static IPAddress IpLocal => OSSupportsLocalIPv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback;

        public static IPAddress IpAny => OSSupportsLocalIPv6 ? IPAddress.IPv6Any : IPAddress.Any;

        public static Configuration GuiConfig;

        public static MainController Controller;

        public static MenuViewController ViewController;

        public static UpdateNode UpdateNodeChecker;

        public static UpdateSubscribeManager UpdateSubscribeManager;

        public static Configuration LoadFile(string filename)
        {
            Configuration config;
            try
            {
                if (File.Exists(filename))
                {
                    var configContent = File.ReadAllText(filename);
                    config = Load(configContent);
                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            config = new Configuration();
            config.FixConfiguration();
            return config;
        }

        public static Configuration Load()
        {
            return LoadFile(ConfigFile);
        }

        private static Configuration Load(string configStr)
        {
            try
            {
                var config = JsonSerializer.Deserialize<Configuration>(configStr);
                if (config is not null)
                {
                    config.FixConfiguration();
                    return config;
                }
            }
            catch
            {
                // ignored
            }
            return null;
        }

        public static void LoadConfig()
        {
            GuiConfig = Load();
        }

        public static void SaveConfig()
        {
            if (GuiConfig.Index >= GuiConfig.Configs.Count)
            {
                GuiConfig.Index = GuiConfig.Configs.Count - 1;
            }
            else if (GuiConfig.Index < 0)
            {
                GuiConfig.Index = 0;
            }

            try
            {
                var jsonString = JsonUtils.Serialize(GuiConfig, true);
                File.WriteAllText(ConfigFile, jsonString);
            }
            catch (IOException e)
            {
                Console.Error.WriteLine(e);
            }
        }
    }
}
