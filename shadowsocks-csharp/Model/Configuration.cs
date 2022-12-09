using Shadowsocks.Enums;
using Shadowsocks.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;

namespace Shadowsocks.Model
{
    [Serializable]
    public class Configuration : ViewModelBase
    {
        #region private

        private List<Server> _configs;
        private int _index;
        private bool _random;
        private ProxyMode _sysProxyMode;
        private bool _shareOverLan;
        private int _localPort;
        private int _reconnectTimes;
        private BalanceType _balanceType;
        private bool _randomInGroup;
        private int _ttl;
        private int _connectTimeout;
        private ProxyRuleMode _proxyRuleMode;
        private bool _proxyEnable;
        private bool _pacDirectGoProxy;
        private ProxyType _proxyType;
        private string _proxyHost;
        private int _proxyPort;
        private string _proxyAuthUser;
        private string _proxyAuthPass;
        private string _proxyUserAgent;
        private string _authUser;
        private string _authPass;
        private bool _autoBan;
        private bool _checkSwitchAutoCloseAll;
        private bool _logEnable;
        private bool _sameHostForSameTarget;
        private bool _isPreRelease;
        private bool _autoCheckUpdate;
        private string _langName;
        private List<DnsClient> _dnsClients;
        private List<ServerSubscribe> _serverSubscribes;
        private Dictionary<string, PortMapConfig> _portMap;

        #endregion

        #region Public

        /// <summary>
        /// 服务器列表
        /// </summary>
        public List<Server> Configs { get => _configs; set => SetField(ref _configs, value); }

        /// <summary>
        /// 选中的服务器在列表的位置
        /// </summary>
        public int Index { get => _index; set => SetField(ref _index, value); }

        /// <summary>
        /// 是否启用负载均衡
        /// </summary>
        public bool Random { get => _random; set => SetField(ref _random, value); }

        /// <summary>
        /// 系统代理模式
        /// </summary>
        public ProxyMode SysProxyMode { get => _sysProxyMode; set => SetField(ref _sysProxyMode, value); }

        /// <summary>
        /// 是否监听所有网卡
        /// </summary>
        public bool ShareOverLan { get => _shareOverLan; set => SetField(ref _shareOverLan, value); }

        /// <summary>
        /// 监听端口
        /// </summary>
        public int LocalPort { get => _localPort; set => SetField(ref _localPort, value); }

        /// <summary>
        /// 重连次数
        /// </summary>
        public int ReconnectTimes { get => _reconnectTimes; set => SetField(ref _reconnectTimes, value); }

        /// <summary>
        /// 负载均衡使用的算法
        /// </summary>
        public BalanceType BalanceType { get => _balanceType; set => SetField(ref _balanceType, value); }

        /// <summary>
        /// 负载均衡是否只在所选组切换
        /// </summary>
        public bool RandomInGroup { get => _randomInGroup; set => SetField(ref _randomInGroup, value); }

        /// <summary>
        /// 空闲断开间隔（单位：秒）
        /// </summary>
        public int Ttl { get => _ttl; set => SetField(ref _ttl, value); }

        /// <summary>
        /// 连接超时（单位：秒）
        /// </summary>
        public int ConnectTimeout { get => _connectTimeout; set => SetField(ref _connectTimeout, value); }

        /// <summary>
        /// 代理规则模式
        /// </summary>
        public ProxyRuleMode ProxyRuleMode { get => _proxyRuleMode; set => SetField(ref _proxyRuleMode, value); }

        /// <summary>
        /// 是否开启二级代理
        /// </summary>
        public bool ProxyEnable { get => _proxyEnable; set => SetField(ref _proxyEnable, value); }

        /// <summary>
        /// PAC 的直连使用二级代理
        /// </summary>
        public bool PacDirectGoProxy { get => _pacDirectGoProxy; set => SetField(ref _pacDirectGoProxy, value); }

        /// <summary>
        /// 二级代理类型
        /// </summary>
        public ProxyType ProxyType { get => _proxyType; set => SetField(ref _proxyType, value); }

        /// <summary>
        /// 二级代理服务器地址
        /// </summary>
        public string ProxyHost { get => _proxyHost; set => SetField(ref _proxyHost, value); }

        /// <summary>
        /// 二级代理服务器端口
        /// </summary>
        public int ProxyPort { get => _proxyPort; set => SetField(ref _proxyPort, value); }

        /// <summary>
        /// 二级代理用户名
        /// </summary>
        public string ProxyAuthUser { get => _proxyAuthUser; set => SetField(ref _proxyAuthUser, value); }

        /// <summary>
        /// 二级代理密码
        /// </summary>
        public string ProxyAuthPass { get => _proxyAuthPass; set => SetField(ref _proxyAuthPass, value); }

        /// <summary>
        /// Http 请求所用的 UserAgent
        /// </summary>
        public string ProxyUserAgent { get => _proxyUserAgent; set => SetField(ref _proxyUserAgent, value); }

        /// <summary>
        /// 本地代理的用户名
        /// </summary>
        public string AuthUser { get => _authUser; set => SetField(ref _authUser, value); }

        /// <summary>
        /// 本地代理的密码
        /// </summary>
        public string AuthPass { get => _authPass; set => SetField(ref _authPass, value); }

        /// <summary>
        /// 自动禁用出错服务器
        /// </summary>
        public bool AutoBan { get => _autoBan; set => SetField(ref _autoBan, value); }

        /// <summary>
        /// 切换服务器前断开所有连接
        /// </summary>
        public bool CheckSwitchAutoCloseAll { get => _checkSwitchAutoCloseAll; set => SetField(ref _checkSwitchAutoCloseAll, value); }

        /// <summary>
        /// 是否开启日志
        /// </summary>
        public bool LogEnable { get => _logEnable; set => SetField(ref _logEnable, value); }

        /// <summary>
        /// 负载均衡优先使用同一个服务器访问同一地址
        /// </summary>
        public bool SameHostForSameTarget { get => _sameHostForSameTarget; set => SetField(ref _sameHostForSameTarget, value); }

        /// <summary>
        /// 检查更新是否包括测试版更新
        /// </summary>
        public bool IsPreRelease { get => _isPreRelease; set => SetField(ref _isPreRelease, value); }

        /// <summary>
        /// 自动检查更新
        /// </summary>
        public bool AutoCheckUpdate { get => _autoCheckUpdate; set => SetField(ref _autoCheckUpdate, value); }

        /// <summary>
        /// 所选的语言
        /// </summary>
        public string LangName { get => _langName; set => SetField(ref _langName, value); }

        /// <summary>
        /// 自定义的 DNS
        /// </summary>
        public List<DnsClient> DnsClients { get => _dnsClients; set => SetField(ref _dnsClients, value); }

        /// <summary>
        /// 订阅列表
        /// </summary>
        public List<ServerSubscribe> ServerSubscribes { get => _serverSubscribes; set => SetField(ref _serverSubscribes, value); }

        /// <summary>
        /// 端口设置列表
        /// </summary>
        public Dictionary<string, PortMapConfig> PortMap { get => _portMap; set => SetField(ref _portMap, value); }

        #endregion

        #region NotConfig

        private const int KeepVisitTime = 1800;

        private readonly Dictionary<int, ServerSelectStrategy> _serverStrategyMap = new();

        [JsonIgnore]
        public Dictionary<int, PortMapConfigCache> PortMapCache { get; private set; } = new();

        private readonly LRUCache<string, UriVisitTime> _uriCache = new(180);

        #endregion

        public bool KeepCurrentServer(int port, string targetAddr, string id)
        {
            if (SameHostForSameTarget && targetAddr != null)
            {
                lock (_serverStrategyMap)
                {
                    if (!_serverStrategyMap.ContainsKey(port))
                    {
                        _serverStrategyMap[port] = new ServerSelectStrategy();
                    }

                    if (_uriCache.ContainsKey(targetAddr))
                    {
                        var visit = _uriCache.Get(targetAddr);
                        var j = -1;
                        for (var i = 0; i < Configs.Count; ++i)
                        {
                            if (Configs[i].Id == id)
                            {
                                j = i;
                                break;
                            }
                        }
                        if (j >= 0 && visit.index == j && Configs[j].Enable)
                        {
                            _uriCache.Del(targetAddr);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public Server GetCurrentServer(int port, ServerSelectStrategy.FilterFunc filter, string targetAddr = null, bool cfgRandom = false, bool usingRandom = false, bool forceRandom = false)
        {
            lock (_serverStrategyMap)
            {
                if (!_serverStrategyMap.ContainsKey(port))
                {
                    _serverStrategyMap[port] = new ServerSelectStrategy();
                }

                var serverStrategy = _serverStrategyMap[port];

                _uriCache.SetTimeout(KeepVisitTime);
                _uriCache.Sweep();
                if (SameHostForSameTarget && !forceRandom && targetAddr != null && _uriCache.ContainsKey(targetAddr))
                {
                    var visit = _uriCache.Get(targetAddr);
                    if (visit.index < Configs.Count && Configs[visit.index].Enable && Configs[visit.index].SpeedLog.ErrorContinuousTimes == 0)
                    {
                        _uriCache.Del(targetAddr);
                        return Configs[visit.index];
                    }
                }
                if (forceRandom)
                {
                    int i;
                    if (filter == null && RandomInGroup)
                    {
                        i = serverStrategy.Select(Configs, Index, BalanceType, delegate (Server server, Server selServer)
                        {
                            if (selServer != null)
                            {
                                return selServer.Group == server.Group;
                            }

                            return false;
                        }, true);
                    }
                    else
                    {
                        i = serverStrategy.Select(Configs, Index, BalanceType, filter, true);
                    }
                    return i == -1 ? GetErrorServer() : Configs[i];
                }

                if (usingRandom && cfgRandom)
                {
                    int i;
                    if (filter == null && RandomInGroup)
                    {
                        i = serverStrategy.Select(Configs, Index, BalanceType, delegate (Server server, Server selServer)
                        {
                            if (selServer != null)
                            {
                                return selServer.Group == server.Group;
                            }

                            return false;
                        });
                    }
                    else
                    {
                        i = serverStrategy.Select(Configs, Index, BalanceType, filter);
                    }
                    if (i == -1)
                    {
                        return GetErrorServer();
                    }

                    if (targetAddr != null)
                    {
                        var visit = new UriVisitTime
                        {
                            uri = targetAddr,
                            index = i,
                            visitTime = DateTime.Now
                        };
                        _uriCache.Set(targetAddr, visit);
                    }
                    return Configs[i];
                }

                if (Index >= 0 && Index < Configs.Count)
                {
                    var selIndex = Index;
                    if (usingRandom)
                    {
                        foreach (var unused in Configs)
                        {
                            if (Configs[selIndex].Enable)
                            {
                                break;
                            }

                            selIndex = (selIndex + 1) % Configs.Count;
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
                        _uriCache.Set(targetAddr, visit);
                    }
                    return Configs[selIndex];
                }

                return GetErrorServer();
            }
        }

        public void FlushPortMapCache()
        {
            PortMapCache = new Dictionary<int, PortMapConfigCache>();
            var id2server = new Dictionary<string, Server>();
            var server_group = new Dictionary<string, int>();
            foreach (var s in Configs)
            {
                id2server[s.Id] = s;
                if (!string.IsNullOrEmpty(s.Group))
                {
                    server_group[s.Group] = 1;
                }
            }
            foreach (var pair in PortMap)
            {
                int key;
                var pm = pair.Value;
                if (!pm.Enable)
                {
                    continue;
                }

                if (id2server.ContainsKey(pm.Id) || server_group.ContainsKey(pm.Id) || pm.Id == null || pm.Id.Length == 0)
                { }
                else
                {
                    continue;
                }

                try
                {
                    key = int.Parse(pair.Key);
                }
                catch (FormatException)
                {
                    continue;
                }
                PortMapCache[key] = new PortMapConfigCache
                {
                    type = pm.Type,
                    id = pm.Id,
                    server = id2server.ContainsKey(pm.Id) ? id2server[pm.Id] : null,
                    server_addr = pm.Server_addr,
                    server_port = pm.Server_port
                };
            }
            lock (_serverStrategyMap)
            {
                var remove_ports = new List<int>();
                foreach (var pair in _serverStrategyMap)
                {
                    if (PortMapCache.ContainsKey(pair.Key))
                    {
                        continue;
                    }

                    remove_ports.Add(pair.Key);
                }
                foreach (var port in remove_ports)
                {
                    _serverStrategyMap.Remove(port);
                }
                if (!PortMapCache.ContainsKey(LocalPort))
                {
                    _serverStrategyMap.Remove(LocalPort);
                }
            }

            _uriCache.Clear();
        }

        public Configuration()
        {
            Configs = new List<Server>();
            Index = 0;
            Random = false;
            SysProxyMode = ProxyMode.NoModify;
            ShareOverLan = false;
            LocalPort = 1080;
            ReconnectTimes = 2;
            BalanceType = BalanceType.LowException;
            RandomInGroup = true;
            Ttl = 60;
            ConnectTimeout = 5;
            ProxyRuleMode = ProxyRuleMode.Disable;
            ProxyEnable = false;
            PacDirectGoProxy = false;
            ProxyType = ProxyType.Socks5;
            ProxyHost = string.Empty;
            ProxyPort = 1;
            ProxyAuthUser = string.Empty;
            ProxyAuthPass = string.Empty;
            ProxyUserAgent = string.Empty;
            AuthUser = string.Empty;
            AuthPass = string.Empty;
            AutoBan = false;
            CheckSwitchAutoCloseAll = true;
            LogEnable = true;
            SameHostForSameTarget = true;
            IsPreRelease = false;
            AutoCheckUpdate = true;
            LangName = string.Empty;
            DnsClients = new List<DnsClient>
            {
                new(DnsType.DnsOverTls) { DnsServer = @"208.67.222.222" },
                new(DnsType.DnsOverTls) { DnsServer = @"208.67.220.220" },
                new(DnsType.DnsOverTls) { DnsServer = @"1.1.1.1" },
                new(DnsType.DnsOverTls) { DnsServer = @"1.0.0.1" },
                new(DnsType.DnsOverTls) { DnsServer = @"1.12.12.12" },
            };
            ServerSubscribes = new List<ServerSubscribe>();
            PortMap = new Dictionary<string, PortMapConfig>();
        }

        public void CopyFrom(Configuration config)
        {
            Configs = config.Configs;
            Index = config.Index;
            Random = config.Random;
            SysProxyMode = config.SysProxyMode;
            ShareOverLan = config.ShareOverLan;
            LocalPort = config.LocalPort;
            ReconnectTimes = config.ReconnectTimes;
            BalanceType = config.BalanceType;
            RandomInGroup = config.RandomInGroup;
            Ttl = config.Ttl;
            ConnectTimeout = config.ConnectTimeout;
            ProxyRuleMode = config.ProxyRuleMode;
            ProxyEnable = config.ProxyEnable;
            PacDirectGoProxy = config.PacDirectGoProxy;
            ProxyType = config.ProxyType;
            ProxyHost = config.ProxyHost;
            ProxyPort = config.ProxyPort;
            ProxyAuthUser = config.ProxyAuthUser;
            ProxyAuthPass = config.ProxyAuthPass;
            ProxyUserAgent = config.ProxyUserAgent;
            AuthUser = config.AuthUser;
            AuthPass = config.AuthPass;
            AutoBan = config.AutoBan;
            CheckSwitchAutoCloseAll = config.CheckSwitchAutoCloseAll;
            LogEnable = config.LogEnable;
            SameHostForSameTarget = config.SameHostForSameTarget;
            IsPreRelease = config.IsPreRelease;
            AutoCheckUpdate = config.AutoCheckUpdate;
            LangName = config.LangName;
            DnsClients = config.DnsClients;
            ServerSubscribes = config.ServerSubscribes;
            //PortMap = config.PortMap;
        }

        public void FixConfiguration()
        {
            if (!IsPort(LocalPort))
            {
                LocalPort = 1080;
            }
            if (PortMap == null)
            {
                PortMap = new Dictionary<string, PortMapConfig>();
            }
            if (ConnectTimeout == 0)
            {
                ConnectTimeout = 5;
                ReconnectTimes = 2;
                Ttl = 60;
            }
            if (Index < 0 || Index >= Configs.Count)
            {
                Index = 0;
            }
            if (Configs.Count == 0)
            {
                Configs.Add(new Server());
            }

            var id = new HashSet<string>();
            foreach (var server in Configs)
            {
                while (id.Contains(server.Id))
                {
                    server.Id = Guid.NewGuid().ToString(@"N");
                }
                id.Add(server.Id);
            }
        }

        public bool IsDefaultConfig()
        {
            return Configs.All(server => server.server == new Server().server);
        }

        private static Server GetErrorServer()
        {
            var server = new Server { server = @"invalid" };
            return server;
        }

        private static bool IsPort(int port)
        {
            return port is > IPEndPoint.MinPort and <= IPEndPoint.MaxPort;
        }
    }
}
