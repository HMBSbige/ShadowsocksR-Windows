using Newtonsoft.Json;
using Shadowsocks.Controller;
using Shadowsocks.Encryption;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Text;

namespace Shadowsocks.Model
{
    public class UriVisitTime : IComparable
    {
        public DateTime visitTime;
        public string uri;
        public int index;

        public int CompareTo(object other)
        {
            if (!(other is UriVisitTime))
                throw new InvalidOperationException("CompareTo: Not a UriVisitTime");
            return Equals(other) ? 0 : visitTime.CompareTo(((UriVisitTime)other).visitTime);
        }

    }

    public enum PortMapType
    {
        Forward = 0,
        ForceProxy,
        RuleProxy
    }

    public enum ProxyRuleMode
    {
        Disable = 0,
        BypassLan,
        BypassLanAndChina,
        BypassLanAndNotChina,
        UserCustom = 16
    }

    [Serializable]
    public class PortMapConfig
    {
        public bool enable;
        public PortMapType type;
        public string id;
        public string server_addr;
        public int server_port;
        public string remarks;
    }

    public class PortMapConfigCache
    {
        public PortMapType type;
        public string id;
        public Server server;
        public string server_addr;
        public int server_port;
    }

    [Serializable]
    public class ServerSubscribe
    {
        private static string DEFAULT_FEED_URL = @"https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/freenodeplain.txt";

        public string URL = DEFAULT_FEED_URL;
        public string Group;
        public ulong LastUpdateTime;
    }

    public static class GlobalConfiguration
    {
        public static string config_password = string.Empty;
    }

    [Serializable]
    class ConfigurationException : Exception
    {
        public ConfigurationException()
        { }
        public ConfigurationException(string message) : base(message) { }
        public ConfigurationException(string message, Exception inner) : base(message, inner) { }
        protected ConfigurationException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }

    [Serializable]
    class ConfigurationWarning : Exception
    {
        public ConfigurationWarning()
        { }
        public ConfigurationWarning(string message) : base(message) { }
        public ConfigurationWarning(string message, Exception inner) : base(message, inner) { }
        protected ConfigurationWarning(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }

    [Serializable]
    public class Configuration
    {
        #region Data

        public ObservableCollection<Server> configs;
        public int index;
        public bool random;
        public int sysProxyMode;
        public bool shareOverLan;
        public int localPort;
        public string localAuthPassword;

        public string localDnsServer;
        public string dnsServer;
        public int reconnectTimes;
        public string balanceAlgorithm;
        public bool randomInGroup;
        public int TTL;
        public int connectTimeout;

        public int proxyRuleMode;

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
        public bool logEnable = true;
        public bool sameHostForSameTarget;

        public int keepVisitTime;

        public bool isPreRelease;
        public bool AutoCheckUpdate;

        public bool nodeFeedAutoUpdate;
        public List<ServerSubscribe> serverSubscribes;

        public Dictionary<string, string> token = new Dictionary<string, string>();
        public Dictionary<string, PortMapConfig> portMap = new Dictionary<string, PortMapConfig>();

        #endregion

        private Dictionary<int, ServerSelectStrategy> serverStrategyMap = new Dictionary<int, ServerSelectStrategy>();
        private Dictionary<int, PortMapConfigCache> portMapCache = new Dictionary<int, PortMapConfigCache>();
        private LRUCache<string, UriVisitTime> uricache = new LRUCache<string, UriVisitTime>(180);

        private const string CONFIG_FILE = @"gui-config.json";
        private const string CONFIG_FILE_BACKUP = @"gui-config.json.backup";

        public static void SetPassword(string password)
        {
            GlobalConfiguration.config_password = password;
        }

        public static bool SetPasswordTry(string oldPassword)
        {
            return oldPassword == GlobalConfiguration.config_password;
        }

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

            balanceAlgorithm = @"LowException";
            random = false;
            sysProxyMode = (int)ProxyMode.NoModify;
            proxyRuleMode = (int)ProxyRuleMode.Disable;

            AutoCheckUpdate = true;
            isPreRelease = false;
            nodeFeedAutoUpdate = true;

            serverSubscribes = new List<ServerSubscribe>();

            configs = new ObservableCollection<Server>();
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
            nodeFeedAutoUpdate = config.nodeFeedAutoUpdate;
            serverSubscribes = config.serverSubscribes;
        }

        public void FixConfiguration()
        {
            if (localPort == 0)
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
            if (token == null)
            {
                token = new Dictionary<string, string>();
            }
            if (connectTimeout == 0)
            {
                connectTimeout = 10;
                reconnectTimes = 2;
                TTL = 180;
                keepVisitTime = 180;
            }
            if (localAuthPassword == null || localAuthPassword.Length < 16)
            {
                localAuthPassword = RandString(20);
            }

            var id = new Dictionary<string, int>();
            if (index < 0 || index >= configs.Count) index = 0;
            if (configs.Count == 0)
            {
                configs.Add(GetDefaultServer());
            }
            foreach (var server in configs)
            {
                if (id.ContainsKey(server.Id))
                {
                    var newId = new byte[16];
                    Utils.RandBytes(newId, newId.Length);
                    server.Id = BitConverter.ToString(newId).Replace("-", string.Empty);
                }
                else
                {
                    id[server.Id] = 0;
                }
            }
        }

        private static string RandString(int len)
        {
            const string set = @"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
            var ret = string.Empty;
            var random = new Random();
            for (var i = 0; i < len; ++i)
            {
                ret += set[random.Next(set.Length)];
            }
            return ret;
        }

        public static Configuration LoadFile(string filename)
        {
            try
            {
                if (File.Exists(filename))
                {
                    var configContent = File.ReadAllText(filename);
                    return Load(configContent);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            var config = new Configuration();
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
                if (GlobalConfiguration.config_password.Length > 0)
                {
                    using var encryptor = EncryptorFactory.GetEncryptor(@"aes-256-cfb", GlobalConfiguration.config_password);
                    var cfgData = Encoding.UTF8.GetBytes(jsonString);
                    jsonString = Utils.EncryptLargeBytesToBase64String(encryptor, cfgData);
                }
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
                if (GlobalConfiguration.config_password.Length > 0)
                {
                    using var encryptor = EncryptorFactory.GetEncryptor(@"aes-256-cfb", GlobalConfiguration.config_password);
                    config_str = Encoding.UTF8.GetString(Utils.DecryptLargeBase64StringToBytes(encryptor, config_str));
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                var config = JsonConvert.DeserializeObject<Configuration>(config_str);
                config.FixConfiguration();
                return config;
            }
            catch
            {
                // ignored
            }

            return null;
        }

        public static Server GetDefaultServer()
        {
            return new Server();
        }

        public bool IsDefaultConfig()
        {
            return configs.Count == 1 && configs[0].server == GetDefaultServer().server;
        }

        private static Server GetErrorServer()
        {
            var server = new Server { server = "invalid" };
            return server;
        }

        public static void CheckPort(int port)
        {
            if (port <= IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
            {
                throw new ConfigurationException(I18N.GetString("Port out of range"));
            }
        }

    }

    [Serializable]
    public class ServerTrans
    {
        public long totalUploadBytes;
        public long totalDownloadBytes;
    }

    [Serializable]
    public class ServerTransferTotal
    {
        private const string LOG_FILE = @"transfer_log.json";

        public Dictionary<string, ServerTrans> servers = new Dictionary<string, ServerTrans>();
        private int saveCounter;
        private DateTime saveTime;

        public static ServerTransferTotal Load()
        {
            try
            {
                var config_str = File.ReadAllText(LOG_FILE);
                var config = new ServerTransferTotal();
                try
                {
                    if (GlobalConfiguration.config_password.Length > 0)
                    {
                        using var encryptor = EncryptorFactory.GetEncryptor(@"aes-256-cfb", GlobalConfiguration.config_password);
                        config_str = Encoding.UTF8.GetString(Utils.DecryptLargeBase64StringToBytes(encryptor, config_str));
                    }
                }
                catch
                {
                    // ignored
                }

                config.servers = JsonConvert.DeserializeObject<Dictionary<string, ServerTrans>>(config_str);
                config.Init();
                return config;
            }
            catch (Exception e)
            {
                if (!(e is FileNotFoundException))
                {
                    Console.WriteLine(e);
                }
                return new ServerTransferTotal();
            }
        }

        private void Init()
        {
            saveCounter = 256;
            saveTime = DateTime.Now;
            if (servers == null)
            {
                servers = new Dictionary<string, ServerTrans>();
            }
        }

        public static void Save(ServerTransferTotal config)
        {
            try
            {
                using var sw = new StreamWriter(File.Open(LOG_FILE, FileMode.Create));
                var jsonString = JsonConvert.SerializeObject(config.servers, Formatting.Indented);
                if (GlobalConfiguration.config_password.Length > 0)
                {
                    using var encryptor = EncryptorFactory.GetEncryptor(@"aes-256-cfb", GlobalConfiguration.config_password);
                    var cfgData = Encoding.UTF8.GetBytes(jsonString);
                    jsonString = Utils.EncryptLargeBytesToBase64String(encryptor, cfgData);
                }
                sw.Write(jsonString);
                sw.Flush();
            }
            catch (IOException e)
            {
                Console.Error.WriteLine(e);
            }
        }

        public void Clear(string server)
        {
            lock (servers)
            {
                if (servers.ContainsKey(server))
                {
                    servers[server].totalUploadBytes = 0;
                    servers[server].totalDownloadBytes = 0;
                }
            }
        }

        public void AddUpload(string server, long size)
        {
            lock (servers)
            {
                if (!servers.ContainsKey(server))
                    servers.Add(server, new ServerTrans());
                servers[server].totalUploadBytes += size;
            }
            if (--saveCounter <= 0)
            {
                saveCounter = 256;
                if ((DateTime.Now - saveTime).TotalMinutes > 10)
                {
                    lock (servers)
                    {
                        Save(this);
                        saveTime = DateTime.Now;
                    }
                }
            }
        }

        public void AddDownload(string server, long size)
        {
            lock (servers)
            {
                if (!servers.ContainsKey(server))
                    servers.Add(server, new ServerTrans());
                servers[server].totalDownloadBytes += size;
            }
            if (--saveCounter <= 0)
            {
                saveCounter = 256;
                if ((DateTime.Now - saveTime).TotalMinutes > 10)
                {
                    lock (servers)
                    {
                        Save(this);
                        saveTime = DateTime.Now;
                    }
                }
            }
        }
    }
}
