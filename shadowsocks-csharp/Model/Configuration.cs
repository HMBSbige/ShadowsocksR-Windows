using Newtonsoft.Json;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace Shadowsocks.Model
{
    [Serializable]
    public class Configuration
    {
        #region Data

        public List<Server> configs;
        public int index;
        public bool random;
        public ProxyMode sysProxyMode;
        public bool shareOverLan;
        public int localPort;

        public string localDnsServer;
        public string dnsServer;
        public int reconnectTimes;
        public string balanceAlgorithm;
        public bool randomInGroup;
        public int TTL;
        public int connectTimeout;

        public ProxyRuleMode proxyRuleMode;

        public bool proxyEnable;
        public bool pacDirectGoProxy;
        public int proxyType;
        public string proxyHost;
        public int proxyPort;
        public string proxyAuthUser;
        public string proxyAuthPass;
        public string proxyUserAgent;

        public string authUser;
        public string authPass;

        public bool autoBan;
        public bool checkSwitchAutoCloseAll;
        public bool logEnable;
        public bool sameHostForSameTarget;

        public int keepVisitTime;

        public bool isPreRelease;
        public bool AutoCheckUpdate;

        public string LangName;

        public List<ServerSubscribe> serverSubscribes;

        public Dictionary<string, PortMapConfig> portMap = new Dictionary<string, PortMapConfig>();

        #endregion

        private Dictionary<int, ServerSelectStrategy> serverStrategyMap = new Dictionary<int, ServerSelectStrategy>();
        private Dictionary<int, PortMapConfigCache> portMapCache = new Dictionary<int, PortMapConfigCache>();
        private LRUCache<string, UriVisitTime> uricache = new LRUCache<string, UriVisitTime>(180);

        private const string CONFIG_FILE = @"gui-config.json";
        private const string CONFIG_FILE_BACKUP = @"gui-config.json.backup";

        [JsonIgnore]
        public static string LocalHost => GlobalConfiguration.OSSupportsLocalIPv6
                ? $@"[{IPAddress.IPv6Loopback}]"
                : $@"{IPAddress.Loopback}";

        [JsonIgnore]
        public static string AnyHost => GlobalConfiguration.OSSupportsLocalIPv6 ? $@"[{IPAddress.IPv6Any}]" : $@"{IPAddress.Any}";

        public bool KeepCurrentServer(int port, string targetAddr, string id)
        {
            if (sameHostForSameTarget && targetAddr != null)
            {
                lock (serverStrategyMap)
                {
                    if (!serverStrategyMap.ContainsKey(port))
                        serverStrategyMap[port] = new ServerSelectStrategy();

                    if (uricache.ContainsKey(targetAddr))
                    {
                        var visit = uricache.Get(targetAddr);
                        var j = -1;
                        for (var i = 0; i < configs.Count; ++i)
                        {
                            if (configs[i].Id == id)
                            {
                                j = i;
                                break;
                            }
                        }
                        if (j >= 0 && visit.index == j && configs[j].Enable)
                        {
                            uricache.Del(targetAddr);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public Server GetCurrentServer(int port, ServerSelectStrategy.FilterFunc filter, string targetAddr = null, bool cfgRandom = false, bool usingRandom = false, bool forceRandom = false)
        {
            lock (serverStrategyMap)
            {
                if (!serverStrategyMap.ContainsKey(port))
                    serverStrategyMap[port] = new ServerSelectStrategy();
                var serverStrategy = serverStrategyMap[port];

                uricache.SetTimeout(keepVisitTime);
                uricache.Sweep();
                if (sameHostForSameTarget && !forceRandom && targetAddr != null && uricache.ContainsKey(targetAddr))
                {
                    var visit = uricache.Get(targetAddr);
                    if (visit.index < configs.Count && configs[visit.index].Enable && configs[visit.index].SpeedLog.ErrorContinuousTimes == 0)
                    {
                        uricache.Del(targetAddr);
                        return configs[visit.index];
                    }
                }
                if (forceRandom)
                {
                    int i;
                    if (filter == null && randomInGroup)
                    {
                        i = serverStrategy.Select(configs, index, balanceAlgorithm, delegate (Server server, Server selServer)
                        {
                            if (selServer != null)
                                return selServer.Group == server.Group;
                            return false;
                        }, true);
                    }
                    else
                    {
                        i = serverStrategy.Select(configs, index, balanceAlgorithm, filter, true);
                    }
                    return i == -1 ? GetErrorServer() : configs[i];
                }

                if (usingRandom && cfgRandom)
                {
                    int i;
                    if (filter == null && randomInGroup)
                    {
                        i = serverStrategy.Select(configs, index, balanceAlgorithm, delegate (Server server, Server selServer)
                        {
                            if (selServer != null)
                                return selServer.Group == server.Group;
                            return false;
                        });
                    }
                    else
                    {
                        i = serverStrategy.Select(configs, index, balanceAlgorithm, filter);
                    }
                    if (i == -1) return GetErrorServer();
                    if (targetAddr != null)
                    {
                        var visit = new UriVisitTime
                        {
                            uri = targetAddr,
                            index = i,
                            visitTime = DateTime.Now
                        };
                        uricache.Set(targetAddr, visit);
                    }
                    return configs[i];
                }

                if (index >= 0 && index < configs.Count)
                {
                    var selIndex = index;
                    if (usingRandom)
                    {
                        foreach (var unused in configs)
                        {
                            if (configs[selIndex].Enable)
                            {
                                break;
                            }

                            selIndex = (selIndex + 1) % configs.Count;
                        }
                    }

                    if (targetAddr != null)
                    {
                        var visit = new UriVisitTime
                        {
                            uri = targetAddr,
                            index = selIndex,
                            visitTime = DateTime.Now
                        };
                        uricache.Set(targetAddr, visit);
                    }
                    return configs[selIndex];
                }

                return GetErrorServer();
            }
        }

        public void FlushPortMapCache()
        {
            portMapCache = new Dictionary<int, PortMapConfigCache>();
            var id2server = new Dictionary<string, Server>();
            var server_group = new Dictionary<string, int>();
            foreach (var s in configs)
            {
                id2server[s.Id] = s;
                if (!string.IsNullOrEmpty(s.Group))
                {
                    server_group[s.Group] = 1;
                }
            }
            foreach (var pair in portMap)
            {
                int key;
                var pm = pair.Value;
                if (!pm.enable)
                    continue;
                if (id2server.ContainsKey(pm.id) || server_group.ContainsKey(pm.id) || pm.id == null || pm.id.Length == 0)
                { }
                else
                    continue;
                try
                {
                    key = int.Parse(pair.Key);
                }
                catch (FormatException)
                {
                    continue;
                }
                portMapCache[key] = new PortMapConfigCache
                {
                    type = pm.type,
                    id = pm.id,
                    server = id2server.ContainsKey(pm.id) ? id2server[pm.id] : null,
                    server_addr = pm.server_addr,
                    server_port = pm.server_port
                };
            }
            lock (serverStrategyMap)
            {
                var remove_ports = new List<int>();
                foreach (var pair in serverStrategyMap)
                {
                    if (portMapCache.ContainsKey(pair.Key)) continue;
                    remove_ports.Add(pair.Key);
                }
                foreach (var port in remove_ports)
                {
                    serverStrategyMap.Remove(port);
                }
                if (!portMapCache.ContainsKey(localPort))
                    serverStrategyMap.Remove(localPort);
            }

            uricache.Clear();
        }

        public Dictionary<int, PortMapConfigCache> GetPortMapCache()
        {
            return portMapCache;
        }

        public Configuration()
        {
            index = 0;
            localPort = 1080;

            reconnectTimes = 2;
            keepVisitTime = 180;
            connectTimeout = 5;
            dnsServer = string.Empty;
            localDnsServer = string.Empty;

            balanceAlgorithm = LoadBalance.LowException.ToString();
            random = false;
            sysProxyMode = ProxyMode.NoModify;
            proxyRuleMode = ProxyRuleMode.Disable;

            checkSwitchAutoCloseAll = true;
            logEnable = true;

            AutoCheckUpdate = true;
            isPreRelease = true;

            LangName = string.Empty;

            serverSubscribes = new List<ServerSubscribe>();

            configs = new List<Server>();
        }

        public void CopyFrom(Configuration config)
        {
            configs = config.configs;
            index = config.index;
            random = config.random;
            sysProxyMode = config.sysProxyMode;
            shareOverLan = config.shareOverLan;
            localPort = config.localPort;
            reconnectTimes = config.reconnectTimes;
            balanceAlgorithm = config.balanceAlgorithm;
            randomInGroup = config.randomInGroup;
            TTL = config.TTL;
            connectTimeout = config.connectTimeout;
            dnsServer = config.dnsServer;
            localDnsServer = config.localDnsServer;
            proxyEnable = config.proxyEnable;
            pacDirectGoProxy = config.pacDirectGoProxy;
            proxyType = config.proxyType;
            proxyHost = config.proxyHost;
            proxyPort = config.proxyPort;
            proxyAuthUser = config.proxyAuthUser;
            proxyAuthPass = config.proxyAuthPass;
            proxyUserAgent = config.proxyUserAgent;
            authUser = config.authUser;
            authPass = config.authPass;
            autoBan = config.autoBan;
            checkSwitchAutoCloseAll = config.checkSwitchAutoCloseAll;
            logEnable = config.logEnable;
            sameHostForSameTarget = config.sameHostForSameTarget;
            keepVisitTime = config.keepVisitTime;
            AutoCheckUpdate = config.AutoCheckUpdate;
            isPreRelease = config.isPreRelease;
            serverSubscribes = config.serverSubscribes;
            LangName = config.LangName;
        }

        private void FixConfiguration()
        {
            if (!IsPort(localPort))
            {
                localPort = 1080;
            }
            if (keepVisitTime == 0)
            {
                keepVisitTime = 180;
            }
            if (portMap == null)
            {
                portMap = new Dictionary<string, PortMapConfig>();
            }
            if (connectTimeout == 0)
            {
                connectTimeout = 10;
                reconnectTimes = 2;
                TTL = 180;
                keepVisitTime = 180;
            }
            if (index < 0 || index >= configs.Count)
            {
                index = 0;
            }
            if (configs.Count == 0)
            {
                configs.Add(GetDefaultServer());
            }

            var id = new HashSet<string>();
            foreach (var server in configs)
            {
                while (id.Contains(server.Id))
                {
                    server.Id = Rng.RandId();
                }
                id.Add(server.Id);
            }
        }

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
            return LoadFile(CONFIG_FILE);
        }

        public static void Save(Configuration config)
        {
            if (config.index >= config.configs.Count)
            {
                config.index = config.configs.Count - 1;
            }
            if (config.index < 0)
            {
                config.index = 0;
            }
            try
            {
                var jsonString = JsonConvert.SerializeObject(config, Formatting.Indented);
                using (var sw = new StreamWriter(File.Open(CONFIG_FILE, FileMode.Create)))
                {
                    sw.Write(jsonString);
                    sw.Flush();
                }

                if (File.Exists(CONFIG_FILE_BACKUP))
                {
                    var dt = File.GetLastWriteTimeUtc(CONFIG_FILE_BACKUP);
                    var now = DateTime.Now;
                    if ((now - dt).TotalHours > 4)
                    {
                        File.Copy(CONFIG_FILE, CONFIG_FILE_BACKUP, true);
                    }
                }
                else
                {
                    File.Copy(CONFIG_FILE, CONFIG_FILE_BACKUP, true);
                }
            }
            catch (IOException e)
            {
                Console.Error.WriteLine(e);
            }
        }

        public static Configuration Load(string config_str)
        {
            try
            {
                var config = JsonConvert.DeserializeObject<Configuration>(config_str);
                config.FixConfiguration();
                return config;
            }
            catch
            {
                return null;
            }
        }

        private static Server GetDefaultServer()
        {
            return new Server();
        }

        public bool IsDefaultConfig()
        {
            return configs.All(server => server.server == GetDefaultServer().server);
        }

        private static Server GetErrorServer()
        {
            var server = new Server { server = @"invalid" };
            return server;
        }

        public static void CheckPort(int port)
        {
            if (!IsPort(port))
            {
                throw new ConfigurationException(I18NUtil.GetAppStringValue(@"PortOutOfRange"));
            }
        }

        private static bool IsPort(int port)
        {
            return port > IPEndPoint.MinPort && port <= IPEndPoint.MaxPort;
        }
    }
}
