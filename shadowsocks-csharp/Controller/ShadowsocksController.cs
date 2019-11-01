using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Controller.Service;
using Shadowsocks.Model;
using Shadowsocks.Model.Transfer;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace Shadowsocks.Controller
{
    public class ShadowsocksController
    {
        // controller:
        // handle user actions
        // manipulates UI
        // interacts with low level logic

        private Listener _listener;
        private List<Listener> _port_map_listener;
        private PACDaemon _pacDaemon;
        private PACServer _pacServer;
        private Configuration _config;
        private ServerTransferTotal _transfer;
        private HostDaemon _hostDaemon;
        private IPRangeSet _chnRangeSet;
        private HttpProxyRunner privoxyRunner;
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
        public event EventHandler ShowSubscribeWindowEvent;

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
                if (_transfer.Servers.TryGetValue(server.Id, out var st))
                {
                    var log = new ServerSpeedLog(st.TotalUploadBytes, st.TotalDownloadBytes);
                    server.SpeedLog = log;
                }
            }

            StartReleasingMemory();
        }

        public bool IsDefaultConfig()
        {
            return _config?.IsDefaultConfig() ?? false;
        }

        private void ReportError(Exception e)
        {
            Errored?.Invoke(this, new ErrorEventArgs(e));
        }

        private void ReloadIPRange()
        {
            _chnRangeSet = new IPRangeSet();
            _chnRangeSet.LoadChn();
            if (_config.proxyRuleMode == ProxyRuleMode.BypassLanAndNotChina)
            {
                _chnRangeSet.Reverse();
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

        private static int FindFirstMatchServer(Server server, IReadOnlyList<Server> servers)
        {
            for (var i = 0; i < servers.Count; ++i)
            {
                if (server.IsMatchServer(servers[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        private static void AppendConfiguration(Configuration mergeConfig, IReadOnlyList<Server> servers)
        {
            if (servers != null)
            {
                Application.Current.Dispatcher?.Invoke(() =>
                {
                    foreach (var server in servers)
                    {
                        if (FindFirstMatchServer(server, mergeConfig.configs) == -1)
                        {
                            mergeConfig.configs.Add(server);
                        }
                    }
                });
            }
        }

        private static IEnumerable<Server> MergeConfiguration(Configuration mergeConfig, IReadOnlyList<Server> servers)
        {
            if (servers != null)
            {
                foreach (var server in servers)
                {
                    var i = FindFirstMatchServer(server, mergeConfig.configs);
                    if (i != -1)
                    {
                        var enable = server.Enable;
                        server.CopyServer(mergeConfig.configs[i]);
                        server.Enable = enable;
                    }
                }
            }

            return from t in mergeConfig.configs let j = FindFirstMatchServer(t, servers) where j == -1 select t;
        }

        private static Configuration MergeGetConfiguration(Configuration mergeConfig)
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
            Save();
        }

        public void SaveServersConfig(Configuration config)
        {
            var missingServers = MergeConfiguration(_config, config.configs);
            _config.CopyFrom(config);
            foreach (var s in missingServers)
            {
                s.Connections.CloseAll();
            }
            SelectServerIndex(_config.index);
        }

        public void SaveServersPortMap(Configuration config)
        {
            _config.portMap = config.portMap;
            SelectServerIndex(_config.index);
            _config.FlushPortMapCache();
        }

        public bool AddServerBySsUrl(string ssUrLs, string force_group = null, bool toLast = false)
        {
            try
            {
                var urls = new List<string>();
                Utils.URL_Split(ssUrLs, ref urls);
                var i = 0;
                foreach (var url in urls.Where(url => url.StartsWith(@"ss://", StringComparison.OrdinalIgnoreCase) || url.StartsWith(@"ssr://", StringComparison.OrdinalIgnoreCase)))
                {
                    ++i;
                    var server = new Server(url, force_group);
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
                }
                if (i > 0)
                {
                    Save();
                    return true;
                }
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                return false;
            }
            return false;
        }

        public bool AddSubscribeUrl(string str)
        {
            try
            {
                var urls = str.Split(new[] { "\r\n", "\r", "\n", " " }, StringSplitOptions.RemoveEmptyEntries);
                var i = 0;
                foreach (var url in urls.Where(url => url.StartsWith(@"sub://", StringComparison.OrdinalIgnoreCase)))
                {
                    var sub = Regex.Match(url, "sub://([A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
                    if (sub.Success)
                    {
                        var res = Base64.DecodeUrlSafeBase64(sub.Groups[1].Value);
                        if (_config.serverSubscribes.All(serverSubscribe => serverSubscribe.Url != res))
                        {
                            _config.serverSubscribes.Add(new ServerSubscribe { Url = res });
                            ++i;
                        }
                    }
                }
                if (i > 0)
                {
                    Save();
                    return true;
                }
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                return false;
            }
            return false;
        }

        public void ToggleMode(ProxyMode mode)
        {
            _config.sysProxyMode = mode;
            Save();
            Application.Current.Dispatcher?.Invoke(() => { ToggleModeChanged?.Invoke(this, new EventArgs()); });
        }

        public void ToggleRuleMode(ProxyRuleMode mode)
        {
            _config.proxyRuleMode = mode;
            Save();
            Application.Current.Dispatcher?.Invoke(() => { ToggleRuleModeChanged?.Invoke(this, new EventArgs()); });
        }

        public void ToggleSelectRandom(bool enabled)
        {
            _config.random = enabled;
            Save();
        }

        public void ToggleSameHostForSameTargetRandom(bool enabled)
        {
            _config.sameHostForSameTarget = enabled;
            Save();
        }

        public void ToggleSelectAutoCheckUpdate(bool enabled)
        {
            _config.AutoCheckUpdate = enabled;
            Save();
        }

        public void ToggleSelectAllowPreRelease(bool enabled)
        {
            _config.isPreRelease = enabled;
            Save();
        }

        public void SelectServerIndex(int index)
        {
            _config.index = index;
            Save();
        }

        public void Save()
        {
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
            privoxyRunner?.Stop();
            if (_config.sysProxyMode != ProxyMode.NoModify && _config.sysProxyMode != ProxyMode.Direct)
            {
                SystemProxy.SystemProxy.Update(_config, true, null);
            }
            ServerTransferTotal.Save(_transfer, _config.configs);
        }

        public void ClearTransferTotal(string serverId)
        {
            _transfer.Clear(serverId);
            var server = _config.configs.Find(s => s.Id == serverId);
            server?.SpeedLog.ClearTrans();
        }

        public void TouchPACFile()
        {
            PACFileReadyToOpen?.Invoke(this, new PathEventArgs { Path = _pacDaemon.TouchPACFile() });
        }

        public void TouchUserRuleFile()
        {
            UserRuleFileReadyToOpen?.Invoke(this, new PathEventArgs { Path = _pacDaemon.TouchUserRuleFile() });
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

        public void Reload()
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

            if (_hostDaemon == null)
            {
                _hostDaemon = new HostDaemon();
                _hostDaemon.ChnIpChanged += (o, args) => ReloadIPRange();
                _hostDaemon.UserRuleChanged += (o, args) => HostMap.Reload();
            }
            ReloadIPRange();
            HostMap.Reload();

            GlobalConfiguration.OSSupportsLocalIPv6 = Socket.OSSupportsIPv6;

            if (privoxyRunner == null)
            {
                privoxyRunner = new HttpProxyRunner();
            }
            if (_pacDaemon == null)
            {
                _pacDaemon = new PACDaemon();
                _pacDaemon.PACFileChanged += (o, args) => UpdateSystemProxy();
                _pacDaemon.UserRuleFileChanged += PacDaemon_UserRuleFileChanged;
            }
            if (_pacServer == null)
            {
                _pacServer = new PACServer(_pacDaemon);
            }
            _pacServer.UpdatePacUrl(_config);
            if (gfwListUpdater == null)
            {
                gfwListUpdater = new GFWListUpdater();
                gfwListUpdater.UpdateCompleted += (o, args) => UpdatePACFromGFWListCompleted?.Invoke(o, args);
                gfwListUpdater.Error += (o, args) => UpdatePACFromGFWListError?.Invoke(o, args);
            }
            if (chnDomainsAndIPUpdater == null)
            {
                chnDomainsAndIPUpdater = new ChnDomainsAndIPUpdater();
                chnDomainsAndIPUpdater.UpdateCompleted += (o, args) => UpdatePACFromChnDomainsAndIPCompleted?.Invoke(o, args);
                chnDomainsAndIPUpdater.Error += (o, args) => UpdatePACFromGFWListError?.Invoke(o, args);
            }

            _listener?.Stop();

            privoxyRunner.Stop();
            // don't put privoxyRunner.Start() before pacServer.Stop()
            // or bind will fail when switching bind address from 0.0.0.0 to 127.0.0.1
            // though UseShellExecute is set to true now
            // http://stackoverflow.com/questions/10235093/socket-doesnt-close-after-application-exits-if-a-launched-process-is-open
            try
            {
                privoxyRunner.Start(_config);

                var local = new Local(_config, _transfer, _chnRangeSet);
                var services = new List<Listener.IService>
                {
                    local,
                    _pacServer,
                    new HttpPortForwarder(privoxyRunner.RunningPort, _config)
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
                    switch (se.SocketErrorCode)
                    {
                        case SocketError.AddressAlreadyInUse:
                        {
                            e = new Exception(string.Format(I18NUtil.GetAppStringValue(@"PortInUse"), _config.localPort), se);
                            break;
                        }
                        case SocketError.AccessDenied:
                        {
                            e = new Exception(string.Format(I18NUtil.GetAppStringValue(@"PortReserved"), _config.localPort), se);
                            break;
                        }
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
                    var local = new Local(_config, _transfer, _chnRangeSet);
                    var services = new List<Listener.IService> { local };
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
                        switch (se.SocketErrorCode)
                        {
                            case SocketError.AddressAlreadyInUse:
                                e = new Exception(string.Format(I18NUtil.GetAppStringValue(@"PortInUse"), pair.Key), e);
                                break;
                            case SocketError.AccessDenied:
                                e = new Exception(string.Format(I18NUtil.GetAppStringValue(@"PortReserved"), pair.Key), se);
                                break;
                        }
                    }
                    Logging.LogUsefulException(e);
                    ReportError(e);
                }
            }

            Application.Current.Dispatcher?.Invoke(() => { ConfigChanged?.Invoke(this, new EventArgs()); });

            UpdateSystemProxy();
            Utils.ReleaseMemory();
        }

        private void SaveConfig(Configuration newConfig)
        {
            Configuration.Save(newConfig);
            Reload();
        }

        private void UpdateSystemProxy()
        {
            if (_config.sysProxyMode != ProxyMode.NoModify)
            {
                SystemProxy.SystemProxy.Update(_config, false, _pacServer);
            }
        }

        private void PacDaemon_UserRuleFileChanged(object sender, EventArgs e)
        {
            if (!Utils.IsGFWListPAC(PACDaemon.PAC_FILE))
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

        public void ShowConfigForm(int? index = null)
        {
            ShowConfigFormEvent?.Invoke(index, new EventArgs());
        }

        public void ShowSubscribeWindow()
        {
            ShowSubscribeWindowEvent?.Invoke(default, default);
        }

        /// <summary>
        /// Disconnect all connections from the remote host.
        /// </summary>
        public void DisconnectAllConnections(bool checkSwitchAutoCloseAll = false)
        {
            var config = GetCurrentConfiguration();
            if (checkSwitchAutoCloseAll && !config.checkSwitchAutoCloseAll)
            {
                Console.WriteLine(@"config.checkSwitchAutoCloseAll:False");
                return;
            }
            foreach (var server in config.configs)
            {
                server.Connections.CloseAll();
            }
        }

        public void CopyPacUrl()
        {
            Clipboard.SetDataObject(_pacServer.PacUrl);
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
