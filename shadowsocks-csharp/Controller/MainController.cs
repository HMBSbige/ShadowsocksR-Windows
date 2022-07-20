using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Controller.Service;
using Shadowsocks.Enums;
using Shadowsocks.Model;
using Shadowsocks.Model.Transfer;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Windows;

namespace Shadowsocks.Controller
{
    public class MainController
    {
        // controller:
        // handle user actions
        // manipulates UI
        // interacts with low level logic

        private Listener _listener;
        private List<Listener> _portMapListener;
        private PACDaemon _pacDaemon;
        private PACServer _pacServer;

        private readonly ServerTransferTotal _transfer;
        private HostDaemon _hostDaemon;
        private IPRangeSet _chnRangeSet;
        private HttpProxyRunner _httpProxyRunner;
        private GfwListUpdater _gfwListUpdater;
        private bool _stopped;

        public class PathEventArgs : EventArgs
        {
            public string Path;
        }

        #region Event

        public event EventHandler ConfigChanged;
        public event EventHandler ShowConfigFormEvent;
        public event EventHandler ShowSubscribeWindowEvent;

        // when user clicked Edit PAC, and PAC file has already created
        public event EventHandler<PathEventArgs> PACFileReadyToOpen;
        public event EventHandler<PathEventArgs> UserRuleFileReadyToOpen;

        public event EventHandler<GfwListUpdater.ResultEventArgs> UpdatePACFromGFWListCompleted;

        public event ErrorEventHandler UpdatePACFromGFWListError;

        public event ErrorEventHandler Errored;

        #endregion

        public MainController()
        {
            _transfer = ServerTransferTotal.Load();

            foreach (var server in Global.GuiConfig.Configs)
            {
                if (_transfer.Servers.TryGetValue(server.Id, out var st))
                {
                    var log = new ServerSpeedLog(st.TotalUploadBytes, st.TotalDownloadBytes);
                    server.SpeedLog = log;
                }
            }
        }

        private void ReportError(Exception e)
        {
            Errored?.Invoke(this, new ErrorEventArgs(e));
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
                Application.Current.Dispatcher?.InvokeAsync(() =>
                {
                    foreach (var server in servers)
                    {
                        if (FindFirstMatchServer(server, mergeConfig.Configs) == -1)
                        {
                            mergeConfig.Configs.Add(server);
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
                    var i = FindFirstMatchServer(server, mergeConfig.Configs);
                    if (i != -1)
                    {
                        var enable = server.Enable;
                        server.CopyServer(mergeConfig.Configs[i]);
                        server.Enable = enable;
                    }
                }
            }

            return from t in mergeConfig.Configs let j = FindFirstMatchServer(t, servers) where j == -1 select t;
        }

        private static Configuration MergeGetConfiguration(Configuration mergeConfig)
        {
            var ret = Global.Load();
            if (mergeConfig != null)
            {
                MergeConfiguration(mergeConfig, ret.Configs);
            }
            return ret;
        }

        /// <summary>
        /// 从配置文件导入服务器
        /// </summary>
        /// <param name="mergeConfig"></param>
        public void MergeConfiguration(Configuration mergeConfig)
        {
            AppendConfiguration(Global.GuiConfig, mergeConfig.Configs);
            SaveAndReload();
        }

        public void SaveServersConfig(Configuration config, bool reload)
        {
            var missingServers = MergeConfiguration(Global.GuiConfig, config.Configs);
            Global.GuiConfig.CopyFrom(config);
            foreach (var s in missingServers)
            {
                s.Connections.CloseAll();
            }

            if (reload)
            {
                SaveAndReload();
            }
            else
            {
                SaveAndNotifyChanged();
            }
        }

        public void SaveServersPortMap(Configuration config)
        {
            StopPortMap();
            Global.GuiConfig.PortMap = config.PortMap;
            Global.GuiConfig.FlushPortMapCache();
            LoadPortMap();
            SaveAndNotifyChanged();
        }

        /// <summary>
        /// 选择指定服务器
        /// </summary>
        public void SelectServerIndex(int index)
        {
            Global.GuiConfig.Index = index;
            SaveAndNotifyChanged();
        }

        /// <summary>
        /// 导入服务器链接
        /// </summary>
        public bool AddServerBySsUrl(string ssUrLs, string force_group = null, bool toLast = false)
        {
            try
            {
                var urls = ssUrLs.GetLines().Reverse();
                var i = 0;
                foreach (var url in urls.Select(url => url.Trim('/')).Where(url => url.StartsWith(@"ss://", StringComparison.OrdinalIgnoreCase) || url.StartsWith(@"ssr://", StringComparison.OrdinalIgnoreCase)))
                {
                    ++i;
                    var server = new Server(url, force_group);
                    if (toLast)
                    {
                        Global.GuiConfig.Configs.Add(server);
                    }
                    else
                    {
                        var index = Global.GuiConfig.Index + 1;
                        if (index < 0 || index > Global.GuiConfig.Configs.Count)
                        {
                            index = Global.GuiConfig.Configs.Count;
                        }

                        Global.GuiConfig.Configs.Insert(index, server);
                    }
                }
                if (i > 0)
                {
                    SaveAndReload();
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

        /// <summary>
        /// 导入订阅链接
        /// </summary>
        public bool AddSubscribeUrl(string str)
        {
            try
            {
                var urls = str.GetLines();
                var newSubscribes = new List<ServerSubscribe>();
                var existSubscribes = new List<ServerSubscribe>();
                foreach (var url in urls.Where(url => url.StartsWith(@"sub://", StringComparison.OrdinalIgnoreCase)))
                {
                    var sub = Regex.Match(url, "sub://([A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
                    if (sub.Success)
                    {
                        var res = Base64.DecodeUrlSafeBase64(sub.Groups[1].Value);
                        if (Global.GuiConfig.ServerSubscribes.All(serverSubscribe => serverSubscribe.Url != res))
                        {
                            var newSub = new ServerSubscribe { Url = res };
                            newSubscribes.Add(newSub);
                            Global.GuiConfig.ServerSubscribes.Add(newSub);
                        }
                        else
                        {
                            existSubscribes.Add(Global.GuiConfig.ServerSubscribes.Find(serverSubscribe => serverSubscribe.Url == res));
                        }
                    }
                }
                if (newSubscribes.Count > 0)
                {
                    SaveAndNotifyChanged();
                    Global.UpdateSubscribeManager.CreateTask(Global.GuiConfig, Global.UpdateNodeChecker, true, newSubscribes);
                    return true;
                }
                if (existSubscribes.Count > 0)
                {
                    Global.UpdateSubscribeManager.CreateTask(Global.GuiConfig, Global.UpdateNodeChecker, true, existSubscribes);
                    return false;
                }
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                return false;
            }
            return false;
        }

        /// <summary>
        /// 切换系统代理模式
        /// </summary>
        public void ToggleMode(ProxyMode mode)
        {
            ProxyMode oldMode = Global.GuiConfig.SysProxyMode;
            Global.GuiConfig.SysProxyMode = mode;
            ReloadPacServer();
            if (oldMode is not ProxyMode.NoModify && mode is ProxyMode.NoModify)
            {
                SystemProxy.Restore();
            }
            else
            {
                UpdateSystemProxy();
            }
            SaveAndNotifyChanged();
        }

        /// <summary>
        /// 切换代理规则
        /// </summary>
        /// <param name="mode"></param>
        public void ToggleRuleMode(ProxyRuleMode mode)
        {
            Global.GuiConfig.ProxyRuleMode = mode;
            SaveAndNotifyChanged();
        }

        public void ToggleSelectRandom(bool enabled)
        {
            Global.GuiConfig.Random = enabled;
            if (!enabled)
            {
                DisconnectAllConnections(true);
            }
            SaveAndNotifyChanged();
        }

        public void ToggleSameHostForSameTargetRandom(bool enabled)
        {
            Global.GuiConfig.SameHostForSameTarget = enabled;
            SaveAndNotifyChanged();
        }

        public void ToggleSelectAutoCheckUpdate(bool enabled)
        {
            Global.GuiConfig.AutoCheckUpdate = enabled;
            Global.SaveConfig();
        }

        public void ToggleSelectAllowPreRelease(bool enabled)
        {
            Global.GuiConfig.IsPreRelease = enabled;
            Global.SaveConfig();
        }

        /// <summary>
        /// 保存配置文件并通知配置改变
        /// </summary>
        public void SaveAndNotifyChanged()
        {
            Global.SaveConfig();
            Application.Current.Dispatcher?.InvokeAsync(() => { ConfigChanged?.Invoke(this, EventArgs.Empty); });
        }

        /// <summary>
        /// 保存配置文件并重载
        /// </summary>
        private void SaveAndReload()
        {
            Global.SaveConfig();
            Reload();
        }

        private void StopPortMap()
        {
            if (_portMapListener != null)
            {
                foreach (var l in _portMapListener)
                {
                    l.Stop();
                }

                _portMapListener = null;
            }
        }

        private void LoadPortMap()
        {
            _portMapListener = new List<Listener>();
            foreach (var pair in Global.GuiConfig.PortMapCache)
            {
                try
                {
                    var local = new Local(Global.GuiConfig, _transfer, _chnRangeSet);
                    var services = new List<Listener.IService> { local };
                    var listener = new Listener(services);
                    listener.Start(Global.GuiConfig, pair.Key);
                    _portMapListener.Add(listener);
                }
                catch (Exception e)
                {
                    ThrowSocketException(ref e);
                    Logging.LogUsefulException(e);
                    ReportError(e);
                }
            }
        }

        public void Stop()
        {
            if (_stopped)
            {
                return;
            }
            _stopped = true;

            StopPortMap();

            _listener?.Stop();
            _httpProxyRunner?.Stop();
            if (Global.GuiConfig.SysProxyMode is not ProxyMode.NoModify)
            {
                SystemProxy.Restore();
            }
            ServerTransferTotal.Save(_transfer, Global.GuiConfig.Configs);
        }

        public void ClearTransferTotal(string serverId)
        {
            _transfer.Clear(serverId);
            var server = Global.GuiConfig.Configs.Find(s => s.Id == serverId);
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
            _gfwListUpdater?.UpdatePacFromGfwList(Global.GuiConfig);
        }

        public void UpdatePACFromOnlinePac(string url)
        {
            _gfwListUpdater?.UpdateOnlinePac(Global.GuiConfig, url);
        }

        private void ReloadPacServer()
        {
            if (_pacDaemon == null)
            {
                _pacDaemon = new PACDaemon();
                _pacDaemon.PACFileChanged += (o, args) =>
                {
                    _pacServer?.UpdatePacUrl(Global.GuiConfig);
                    UpdateSystemProxy();
                };
                _pacDaemon.UserRuleFileChanged += PacDaemon_UserRuleFileChanged;
            }

            if (_pacServer == null)
            {
                _pacServer = new PACServer(_pacDaemon);
            }

            _pacServer.UpdatePacUrl(Global.GuiConfig);
        }

        private void ReloadIPRange()
        {
            _chnRangeSet = new IPRangeSet();
            _chnRangeSet.LoadChn();
        }

        private void ReloadProxyRule()
        {
            if (_hostDaemon == null)
            {
                _hostDaemon = new HostDaemon();
                _hostDaemon.ChnIpChanged += (o, args) => ReloadIPRange();
                _hostDaemon.UserRuleChanged += (o, args) => HostMap.Reload();
            }

            ReloadIPRange();
            HostMap.Reload();
        }

        public void Reload()
        {
            StopPortMap();
            // some logic in configuration updated the config when saving, we need to read it again
            Global.GuiConfig = MergeGetConfiguration(Global.GuiConfig);
            Global.GuiConfig.FlushPortMapCache();
            Logging.SaveToFile = Global.GuiConfig.LogEnable;
            Logging.OpenLogFile();

            ReloadProxyRule();

            _httpProxyRunner ??= new HttpProxyRunner();
            ReloadPacServer();
            if (_gfwListUpdater == null)
            {
                _gfwListUpdater = new GfwListUpdater();
                _gfwListUpdater.UpdateCompleted += (o, args) => UpdatePACFromGFWListCompleted?.Invoke(o, args);
                _gfwListUpdater.Error += (o, args) => UpdatePACFromGFWListError?.Invoke(o, args);
            }

            _listener?.Stop();
            _httpProxyRunner.Stop();
            try
            {
                _httpProxyRunner.Start(Global.GuiConfig);

                var local = new Local(Global.GuiConfig, _transfer, _chnRangeSet);
                var services = new List<Listener.IService>
                {
                    local,
                    _pacServer,
                    new HttpPortForwarder(_httpProxyRunner.RunningPort, Global.GuiConfig)
                };
                _listener = new Listener(services);
                _listener.Start(Global.GuiConfig, 0);
            }
            catch (Exception e)
            {
                ThrowSocketException(ref e);
                Logging.LogUsefulException(e);
                ReportError(e);
            }

            LoadPortMap();

            Application.Current.Dispatcher?.InvokeAsync(() => { ConfigChanged?.Invoke(this, EventArgs.Empty); });

            UpdateSystemProxy();
        }

        private static void ThrowSocketException(ref Exception e)
        {
            // TODO:translate Microsoft language into human language
            // i.e. An attempt was made to access a socket in a way forbidden by its access permissions => Port already in use
            // https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.socketerror
            if (e is not SocketException se)
            {
                return;
            }

            switch (se.SocketErrorCode)
            {
                case SocketError.AddressAlreadyInUse:
                {
                    e = new Exception(string.Format(I18NUtil.GetAppStringValue(@"PortInUse"), Global.GuiConfig.LocalPort), se);
                    break;
                }
                case SocketError.AccessDenied:
                {
                    e = new Exception(string.Format(I18NUtil.GetAppStringValue(@"PortReserved"), Global.GuiConfig.LocalPort), se);
                    break;
                }
            }
        }

        private void UpdateSystemProxy()
        {
            SystemProxy.Update(Global.GuiConfig, _pacServer);
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
                GfwListUpdater.MergeAndWritePacFile(FileManager.NonExclusiveReadAllText(Utils.GetTempPath(PACServer.gfwlist_FILE)));
            }

            UpdateSystemProxy();
        }

        public void ShowConfigForm(int? index = null)
        {
            ShowConfigFormEvent?.Invoke(index, EventArgs.Empty);
        }

        public void ShowSubscribeWindow()
        {
            ShowSubscribeWindowEvent?.Invoke(default, EventArgs.Empty);
        }

        /// <summary>
        /// Disconnect all connections from the remote host.
        /// </summary>
        public void DisconnectAllConnections(bool checkSwitchAutoCloseAll = false)
        {
            var config = Global.GuiConfig;
            if (checkSwitchAutoCloseAll && !config.CheckSwitchAutoCloseAll)
            {
                Console.WriteLine(@"config.checkSwitchAutoCloseAll:False");
                return;
            }
            foreach (var server in config.Configs)
            {
                server.Connections.CloseAll();
            }
        }

        public void CopyPacUrl()
        {
            Clipboard.SetDataObject(_pacServer.PacUrl);
        }
    }
}
