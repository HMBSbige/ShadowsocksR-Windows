using Newtonsoft.Json;
using Shadowsocks.Controller;
using Shadowsocks.Encryption;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

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
        UserCustom = 16,
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
        public ConfigurationException() : base() { }
        public ConfigurationException(string message) : base(message) { }
        public ConfigurationException(string message, Exception inner) : base(message, inner) { }
        protected ConfigurationException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }

    [Serializable]
    class ConfigurationWarning : Exception
    {
        public ConfigurationWarning() : base() { }
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

        public List<Server> configs;
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
        public bool logEnable;
        public bool sameHostForSameTarget;

        public int keepVisitTime;

        public bool isHideTips;

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

        public static bool SetPasswordTry(string old_password, string password)
        {
            return old_password == GlobalConfiguration.config_password;
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
                            if (configs[i].id == id)
                            {
                                j = i;
                                break;
                            }
                        }
                        if (j >= 0 && visit.index == j && configs[j].enable)
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
                    if (visit.index < configs.Count && configs[visit.index].enable && configs[visit.index].ServerSpeedLog().ErrorContinurousTimes == 0)
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
                                return selServer.group == server.group;
                            return false;
                        }, true);
                    }
                    else
                    {
                        i = serverStrategy.Select(configs, index, balanceAlgorithm, filter, true);
                    }
                    return i == -1 ? GetErrorServer() : configs[i];
                }
                else if (usingRandom && cfgRandom)
                {
                    int i;
                    if (filter == null && randomInGroup)
                    {
                        i = serverStrategy.Select(configs, index, balanceAlgorithm, delegate (Server server, Server selServer)
                        {
                            if (selServer != null)
                                return selServer.group == server.group;
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
                else
                {
                    if (index >= 0 && index < configs.Count)
                    {
                        var selIndex = index;
                        if (usingRandom)
                        {
                            foreach (var unused in configs)
                            {
                                if (configs[selIndex].isEnable())
                                {
                                    break;
                                }
                                else
                                {
                                    selIndex = (selIndex + 1) % configs.Count;
                                }
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
                    else
                    {
                        return GetErrorServer();
                    }
                }
            }
        }

        public void FlushPortMapCache()
        {
            portMapCache = new Dictionary<int, PortMapConfigCache>();
            var id2server = new Dictionary<string, Server>();
            var server_group = new Dictionary<string, int>();
            foreach (var s in configs)
            {
                id2server[s.id] = s;
                if (!string.IsNullOrEmpty(s.group))
                {
                    server_group[s.group] = 1;
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

        public static void CheckServer(Server server)
        {
            CheckPort(server.server_port);
            if (server.server_udp_port != 0)
                CheckPort(server.server_udp_port);
            try
            {
                CheckPassword(server.password);
            }
            catch (ConfigurationWarning cw)
            {
                server.password = string.Empty;
                MessageBox.Show(cw.Message, cw.Message, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            CheckServer(server.server);
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

            nodeFeedAutoUpdate = true;

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
            isHideTips = config.isHideTips;
            nodeFeedAutoUpdate = config.nodeFeedAutoUpdate;
            serverSubscribes = config.serverSubscribes;
        }

        private void FixConfiguration()
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
                if (id.ContainsKey(server.id))
                {
                    var newId = new byte[16];
                    Util.Utils.RandBytes(newId, newId.Length);
                    server.id = BitConverter.ToString(newId).Replace("-", string.Empty);
                }
                else
                {
                    id[server.id] = 0;
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
                var configContent = File.ReadAllText(filename);
                return Load(configContent);
            }
            catch (Exception e)
            {
                if (!(e is FileNotFoundException))
                {
                    Console.WriteLine(e);
                }
                return new Configuration();
            }
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
                    var encryptor = EncryptorFactory.GetEncryptor("aes-256-cfb", GlobalConfiguration.config_password);
                    var cfgData = Encoding.UTF8.GetBytes(jsonString);
                    var cfgEncrypt = new byte[cfgData.Length + 128];
                    var dataLen = 0;
                    const int buffer_size = 32768;
                    var input = new byte[buffer_size];
                    var output = new byte[buffer_size + 128];
                    for (var start_pos = 0; start_pos < cfgData.Length; start_pos += buffer_size)
                    {
                        var len = Math.Min(cfgData.Length - start_pos, buffer_size);
                        Buffer.BlockCopy(cfgData, start_pos, input, 0, len);
                        encryptor.Encrypt(input, len, output, out var out_len);
                        Buffer.BlockCopy(output, 0, cfgEncrypt, dataLen, out_len);
                        dataLen += out_len;
                    }
                    jsonString = Convert.ToBase64String(cfgEncrypt, 0, dataLen);
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
                    var cfg_encrypt = Convert.FromBase64String(config_str);
                    var encryptor = EncryptorFactory.GetEncryptor("aes-256-cfb", GlobalConfiguration.config_password);
                    var cfg_data = new byte[cfg_encrypt.Length];
                    var data_len = 0;
                    const int buffer_size = 32768;
                    var input = new byte[buffer_size];
                    var output = new byte[buffer_size + 128];
                    for (var start_pos = 0; start_pos < cfg_encrypt.Length; start_pos += buffer_size)
                    {
                        var len = Math.Min(cfg_encrypt.Length - start_pos, buffer_size);
                        Buffer.BlockCopy(cfg_encrypt, start_pos, input, 0, len);
                        encryptor.Decrypt(input, len, output, out var out_len);
                        Buffer.BlockCopy(output, 0, cfg_data, data_len, out_len);
                        data_len += out_len;
                    }
                    config_str = Encoding.UTF8.GetString(cfg_data, 0, data_len);
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

        public static Server CopyServer(Server server)
        {
            var s = new Server
            {
                server = server.server,
                server_port = server.server_port,
                method = server.method,
                protocol = server.protocol,
                protocolparam = server.protocolparam ?? string.Empty,
                obfs = server.obfs,
                obfsparam = server.obfsparam ?? string.Empty,
                password = server.password,
                remarks = server.remarks,
                group = server.group,
                udp_over_tcp = server.udp_over_tcp,
                server_udp_port = server.server_udp_port
            };
            return s;
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

        private static void CheckPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ConfigurationWarning(I18N.GetString("Password are blank"));
            }
        }

        private static void CheckServer(string server)
        {
            if (string.IsNullOrEmpty(server))
            {
                throw new ConfigurationException(I18N.GetString("Server IP can not be blank"));
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
                        var cfgEncrypt = Convert.FromBase64String(config_str);
                        var encryptor = EncryptorFactory.GetEncryptor("aes-256-cfb", GlobalConfiguration.config_password);
                        var cfgData = new byte[cfgEncrypt.Length];
                        encryptor.Decrypt(cfgEncrypt, cfgEncrypt.Length, cfgData, out var data_len);
                        config_str = Encoding.UTF8.GetString(cfgData, 0, data_len);
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
                using (var sw = new StreamWriter(File.Open(LOG_FILE, FileMode.Create)))
                {
                    var jsonString = JsonConvert.SerializeObject(config.servers, Formatting.Indented);
                    if (GlobalConfiguration.config_password.Length > 0)
                    {
                        var encryptor = EncryptorFactory.GetEncryptor("aes-256-cfb", GlobalConfiguration.config_password);
                        var cfgData = Encoding.UTF8.GetBytes(jsonString);
                        var cfgEncrypt = new byte[cfgData.Length + 128];
                        encryptor.Encrypt(cfgData, cfgData.Length, cfgEncrypt, out var data_len);
                        jsonString = Convert.ToBase64String(cfgEncrypt, 0, data_len);
                    }
                    sw.Write(jsonString);
                    sw.Flush();
                }
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
