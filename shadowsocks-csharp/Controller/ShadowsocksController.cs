using Shadowsocks.Model;
using Shadowsocks.Proxy;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Shadowsocks.Controller
{
    public enum ProxyMode
    {
        NoModify,
        Direct,
        Pac,
        Global,
    }

    public class ShadowsocksController
    {
        // controller:
        // handle user actions
        // manipulates UI
        // interacts with low level logic

        private Listener _listener;
        private List<Listener> _port_map_listener;
        private PACServer _pacServer;
        private Configuration _config;
        private ServerTransferTotal _transfer;
        public IPRangeSet _rangeSet;
#if !_CONSOLE
        private HttpProxyRunner privoxyRunner;
#endif
        private GFWListUpdater gfwListUpdater;
        private ChnDomainsAndIPUpdater chnDomainsAndIPUpdater;
        private bool stopped;

        public class PathEventArgs : EventArgs
        {
            public string Path;
        }

        public event EventHandler ConfigChanged;
        public event EventHandler ToggleModeChanged;
        public event EventHandler ToggleRuleModeChanged;
        //public event EventHandler ShareOverLANStatusChanged;
        public event EventHandler ShowConfigFormEvent;

        // when user clicked Edit PAC, and PAC file has already created
        public event EventHandler<PathEventArgs> PACFileReadyToOpen;
        public event EventHandler<PathEventArgs> UserRuleFileReadyToOpen;

        public event EventHandler<GFWListUpdater.ResultEventArgs> UpdatePACFromGFWListCompleted;
        public event EventHandler<ChnDomainsAndIPUpdater.ResultEventArgs> UpdatePACFromChnDomainsAndIPCompleted;

        public event ErrorEventHandler UpdatePACFromGFWListError;

        public event ErrorEventHandler Errored;

        public ShadowsocksController()
        {
            _config = Configuration.Load();
            _transfer = ServerTransferTotal.Load();

            foreach (var server in _config.configs)
            {
                if (_transfer.servers.TryGetValue(server.server, out var st))
                {
                    var log = new ServerSpeedLog(st.totalUploadBytes, st.totalDownloadBytes);
                    server.SetServerSpeedLog(log);
                }
            }

            StartReleasingMemory();
        }

        public void Start()
        {
            Reload();
        }

        protected void ReportError(Exception e)
        {
            Errored?.Invoke(this, new ErrorEventArgs(e));
        }

        public void ReloadIPRange()
        {
            _rangeSet = new IPRangeSet();
            _rangeSet.LoadChn();
            if (_config.proxyRuleMode == (int)ProxyRuleMode.BypassLanAndNotChina)
            {
                _rangeSet.Reverse();
            }
        }

        // always return copy
        public Configuration GetConfiguration()
        {
            return Configuration.Load();
        }

        public Configuration GetCurrentConfiguration()
        {
            return _config;
        }

        private int FindFirstMatchServer(Server server, List<Server> servers)
        {
            for (var i = 0; i < servers.Count; ++i)
            {
                if (server.isMatchServer(servers[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        public void AppendConfiguration(Configuration mergeConfig, List<Server> servers)
        {
            if (servers != null)
            {
                for (var j = 0; j < servers.Count; ++j)
                {
                    if (FindFirstMatchServer(servers[j], mergeConfig.configs) == -1)
                    {
                        mergeConfig.configs.Add(servers[j]);
                    }
                }
            }
        }

        public List<Server> MergeConfiguration(Configuration mergeConfig, List<Server> servers)
        {
            var missingServers = new List<Server>();
            if (servers != null)
            {
                for (var j = 0; j < servers.Count; ++j)
                {
                    var i = FindFirstMatchServer(servers[j], mergeConfig.configs);
                    if (i != -1)
                    {
                        var enable = servers[j].enable;
                        servers[j].CopyServer(mergeConfig.configs[i]);
                        servers[j].enable = enable;
                    }
                }
            }
            for (var i = 0; i < mergeConfig.configs.Count; ++i)
            {
                var j = FindFirstMatchServer(mergeConfig.configs[i], servers);
                if (j == -1)
                {
                    missingServers.Add(mergeConfig.configs[i]);
                }
            }
            return missingServers;
        }

        public Configuration MergeGetConfiguration(Configuration mergeConfig)
        {
            var ret = Configuration.Load();
            if (mergeConfig != null)
            {
                MergeConfiguration(mergeConfig, ret.configs);
            }
            return ret;
        }

        public void MergeConfiguration(Configuration mergeConfig)
        {
            AppendConfiguration(_config, mergeConfig.configs);
            SaveConfig(_config);
        }

        public bool SaveServersConfig(string config)
        {
            var new_cfg = Configuration.Load(config);
            if (new_cfg != null)
            {
                SaveServersConfig(new_cfg);
                return true;
            }
            return false;
        }

        public void SaveServersConfig(Configuration config)
        {
            var missingServers = MergeConfiguration(_config, config.configs);
            _config.CopyFrom(config);
            foreach (var s in missingServers)
            {
                s.GetConnections().CloseAll();
            }
            SelectServerIndex(_config.index);
        }

        public void SaveServersPortMap(Configuration config)
        {
            _config.portMap = config.portMap;
            SelectServerIndex(_config.index);
            _config.FlushPortMapCache();
        }

        public bool AddServerBySSURL(string ssURL, string force_group = null, bool toLast = false)
        {
            if (ssURL.StartsWith("ss://", StringComparison.OrdinalIgnoreCase) || ssURL.StartsWith("ssr://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var server = new Server(ssURL, force_group);
                    if (toLast)
                    {
                        _config.configs.Add(server);
                    }
                    else
                    {
                        var index = _config.index + 1;
                        if (index < 0 || index > _config.configs.Count)
                            index = _config.configs.Count;
                        _config.configs.Insert(index, server);
                    }
                    SaveConfig(_config);
                    return true;
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public void ToggleMode(ProxyMode mode)
        {
            _config.sysProxyMode = (int)mode;
            SaveConfig(_config);
            ToggleModeChanged?.Invoke(this, new EventArgs());
        }

        public void ToggleRuleMode(int mode)
        {
            _config.proxyRuleMode = mode;
            SaveConfig(_config);
            ToggleRuleModeChanged?.Invoke(this, new EventArgs());
        }

        public void ToggleSelectRandom(bool enabled)
        {
            _config.random = enabled;
            SaveConfig(_config);
        }

        public void ToggleSameHostForSameTargetRandom(bool enabled)
        {
            _config.sameHostForSameTarget = enabled;
            SaveConfig(_config);
        }

        public void SelectServerIndex(int index)
        {
            _config.index = index;
            SaveConfig(_config);
        }

        public void Stop()
        {
            if (stopped)
            {
                return;
            }
            stopped = true;

            if (_port_map_listener != null)
            {
                foreach (var l in _port_map_listener)
                {
                    l.Stop();
                }
                _port_map_listener = null;
            }

            _listener?.Stop();
#if !_CONSOLE
            privoxyRunner?.Stop();
            if (_config.sysProxyMode != (int)ProxyMode.NoModify && _config.sysProxyMode != (int)ProxyMode.Direct)
            {
                SystemProxy.Update(_config, true);
            }
#endif
            ServerTransferTotal.Save(_transfer);
        }

        public void ClearTransferTotal(string server_addr)
        {
            _transfer.Clear(server_addr);
            foreach (var server in _config.configs)
            {
                if (server.server == server_addr)
                {
                    if (_transfer.servers.ContainsKey(server.server))
                    {
                        server.ServerSpeedLog().ClearTrans();
                    }
                }
            }
        }

        public void TouchPACFile()
        {
            PACFileReadyToOpen?.Invoke(this, new PathEventArgs { Path = PACServer.TouchPACFile() });
        }

        public void TouchUserRuleFile()
        {
            UserRuleFileReadyToOpen?.Invoke(this, new PathEventArgs { Path = PACServer.TouchUserRuleFile() });
        }

        public void UpdatePACFromGFWList()
        {
            gfwListUpdater?.UpdatePACFromGFWList(_config);
        }

        public void UpdatePACFromChnDomainsAndIP(ChnDomainsAndIPUpdater.Templates template)
        {
            chnDomainsAndIPUpdater?.UpdatePACFromChnDomainsAndIP(_config, template);
        }

        public void UpdatePACFromOnlinePac(string url)
        {
            gfwListUpdater?.UpdateOnlinePAC(_config, url);
        }

        protected void Reload()
        {
            if (_port_map_listener != null)
            {
                foreach (var l in _port_map_listener)
                {
                    l.Stop();
                }
                _port_map_listener = null;
            }
            // some logic in configuration updated the config when saving, we need to read it again
            _config = MergeGetConfiguration(_config);
            _config.FlushPortMapCache();
            Logging.save_to_file = _config.logEnable;
            Logging.OpenLogFile();
            ReloadIPRange();

            var hostMap = new HostMap();
            hostMap.LoadHostFile();
            HostMap.Instance().Clear(hostMap);

#if !_CONSOLE
            if (privoxyRunner == null)
            {
                privoxyRunner = new HttpProxyRunner();
            }
#endif
            if (_pacServer == null)
            {
                _pacServer = new PACServer();
                _pacServer.PACFileChanged += pacServer_PACFileChanged;
                _pacServer.UserRuleFileChanged += pacServer_UserRuleFileChanged;
            }
            _pacServer.UpdateConfiguration(_config);
            if (gfwListUpdater == null)
            {
                gfwListUpdater = new GFWListUpdater();
                gfwListUpdater.UpdateCompleted += pacServer_PACUpdateCompleted;
                gfwListUpdater.Error += pacServer_PACUpdateError;
            }
            if (chnDomainsAndIPUpdater == null)
            {
                chnDomainsAndIPUpdater = new ChnDomainsAndIPUpdater();
                chnDomainsAndIPUpdater.UpdateCompleted += pacServer_PACUpdateCompleted;
                chnDomainsAndIPUpdater.Error += pacServer_PACUpdateError;
            }

            _listener?.Stop();

            // don't put PrivoxyRunner.Start() before pacServer.Stop()
            // or bind will fail when switching bind address from 0.0.0.0 to 127.0.0.1
            // though UseShellExecute is set to true now
            // http://stackoverflow.com/questions/10235093/socket-doesnt-close-after-application-exits-if-a-launched-process-is-open
            try
            {
#if !_CONSOLE
                privoxyRunner.Stop();
                privoxyRunner.Start(_config);
#endif

                var local = new Local(_config, _transfer, _rangeSet);
                var services = new List<Listener.Service>
                {
                    local,
                    _pacServer,
                    new APIServer(this, _config),
#if !_CONSOLE
                    new HttpPortForwarder(privoxyRunner.RunningPort, _config)
#endif
                };
                _listener = new Listener(services);
                _listener.Start(_config, 0);

            }
            catch (Exception e)
            {
                // translate Microsoft language into human language
                // i.e. An attempt was made to access a socket in a way forbidden by its access permissions => Port already in use
                if (e is SocketException se)
                {
                    if (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        e = new Exception(string.Format(I18N.GetString("Port {0} already in use"), _config.localPort),
                                se);
                    }
                    else if (se.SocketErrorCode == SocketError.AccessDenied)
                    {
                        e = new Exception(string.Format(I18N.GetString("Port {0} is reserved by system"), _config.localPort), se);
                    }
                }

                Logging.LogUsefulException(e);
                ReportError(e);
            }

            _port_map_listener = new List<Listener>();
            foreach (var pair in _config.GetPortMapCache())
            {
                try
                {
                    var local = new Local(_config, _transfer, _rangeSet);
                    var services = new List<Listener.Service> { local };
                    var listener = new Listener(services);
                    listener.Start(_config, pair.Key);
                    _port_map_listener.Add(listener);
                }
                catch (Exception e)
                {
                    // translate Microsoft language into human language
                    // i.e. An attempt was made to access a socket in a way forbidden by its access permissions => Port already in use
                    if (e is SocketException se)
                    {
                        if (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
                        {
                            e = new Exception(string.Format(I18N.GetString("Port {0} already in use"), pair.Key), e);
                        }
                        else if (se.SocketErrorCode == SocketError.AccessDenied)
                        {
                            e = new Exception(string.Format(I18N.GetString("Port {0} is reserved by system"), pair.Key), se);
                        }
                    }
                    Logging.LogUsefulException(e);
                    ReportError(e);
                }
            }

            ConfigChanged?.Invoke(this, new EventArgs());

            UpdateSystemProxy();
            Utils.ReleaseMemory();
        }

        protected void SaveConfig(Configuration newConfig)
        {
            Configuration.Save(newConfig);
            Reload();
        }

        private void UpdateSystemProxy()
        {
#if !_CONSOLE
            if (_config.sysProxyMode != (int)ProxyMode.NoModify)
            {
                SystemProxy.Update(_config, false);
            }
#endif
        }

        private void pacServer_PACFileChanged(object sender, EventArgs e)
        {
            UpdateSystemProxy();
        }

        private void pacServer_UserRuleFileChanged(object sender, EventArgs e)
        {
            if (!Utils.IsGFWListPAC(PACServer.PAC_FILE))
            {
                return;
            }
            if (!File.Exists(Utils.GetTempPath(PACServer.gfwlist_FILE)))
            {
                UpdatePACFromGFWList();
            }
            else
            {
                GFWListUpdater.MergeAndWritePACFile(FileManager.NonExclusiveReadAllText(Utils.GetTempPath(PACServer.gfwlist_FILE)));
            }

            UpdateSystemProxy();
        }

        private void pacServer_PACUpdateCompleted(object sender, GFWListUpdater.ResultEventArgs e)
        {
            UpdatePACFromGFWListCompleted?.Invoke(sender, e);
        }

        private void pacServer_PACUpdateError(object sender, ErrorEventArgs e)
        {
            UpdatePACFromGFWListError?.Invoke(sender, e);
        }

        private void pacServer_PACUpdateCompleted(object sender, ChnDomainsAndIPUpdater.ResultEventArgs e)
        {
            UpdatePACFromChnDomainsAndIPCompleted?.Invoke(sender, e);
        }

        public void ShowConfigForm(int index)
        {
            ShowConfigFormEvent?.Invoke(index, new EventArgs());
        }

        /// <summary>
        /// Disconnect all connections from the remote host.
        /// </summary>
        public void DisconnectAllConnections()
        {
            var config = GetCurrentConfiguration();
            for (var id = 0; id < config.configs.Count; ++id)
            {
                var server = config.configs[id];
                server.GetConnections().CloseAll();
            }
        }

        #region Memory Management

        private static void StartReleasingMemory()
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    Utils.ReleaseMemory(false);
                    Task.Delay(30 * 1000).Wait();
                }
                // ReSharper disable once FunctionNeverReturns
            }, TaskCreationOptions.LongRunning);
        }

        #endregion
    }
}
