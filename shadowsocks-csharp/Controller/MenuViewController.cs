using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Controller.Service;
using Shadowsocks.Enums;
using Shadowsocks.Model;
using Shadowsocks.Properties;
using Shadowsocks.Util;
using Shadowsocks.View;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Brushes = System.Windows.Media.Brushes;
using Rectangle = System.Drawing.Rectangle;

namespace Shadowsocks.Controller
{
    public class MenuViewController
    {
        private class EventParams
        {
            public readonly object sender;
            public readonly EventArgs e;

            public EventParams(object sender, EventArgs e)
            {
                this.sender = sender;
                this.e = e;
            }
        }

        // yes this is just a menu view controller
        // when config form is closed, it moves away from RAM
        // and it should just do anything related to the config form

        private readonly MainController controller;
        private readonly HttpRequest.UpdateChecker updateChecker;

        private readonly TaskbarIcon _notifyIcon;
        private ContextMenu _contextMenu;

        private MenuItem noModifyItem;
        private MenuItem enableItem;
        private MenuItem PACModeItem;
        private MenuItem globalModeItem;

        private MenuItem ruleBypassLan;
        private MenuItem ruleBypassChina;
        private MenuItem ruleBypassNotChina;
        private MenuItem ruleUser;
        private MenuItem ruleDisableBypass;

        private Separator SeparatorItem;
        private MenuItem ServersItem;
        private MenuItem SelectRandomItem;
        private MenuItem sameHostForSameTargetItem;
        private MenuItem _moreMenu;
        private MenuItem _updateMenu;
        private MenuItem UpdateItem;
        private MenuItem AutoCheckUpdateItem;
        private MenuItem AllowPreReleaseItem;
        private ServerConfigWindow _serverConfigWindow;
        private SettingsWindow _settingsWindow;
        private DnsSettingWindow _dnsSettingsWindow;

        #region ServerLogWindow

        private ServerLogWindow _serverLogWindow;
        private WindowStatus _serverLogWindowStatus;

        #endregion

        private PortSettingsWindow _portMapWindow;
        private SubscribeWindow _subScribeWindow;
        private LogWindow _logWindow;
        private string _urlToOpen;
        private System.Timers.Timer timerDelayCheckUpdate;

        private bool configFrom_open;
        private readonly List<EventParams> eventList = new();

        public MenuViewController(MainController controller)
        {
            this.controller = controller;

            LoadMenu();

            controller.ConfigChanged += controller_ConfigChanged;
            controller.PACFileReadyToOpen += controller_FileReadyToOpen;
            controller.UserRuleFileReadyToOpen += controller_FileReadyToOpen;
            controller.Errored += ControllerError;
            controller.UpdatePACFromGFWListCompleted += controller_UpdatePACFromGFWListCompleted;
            controller.UpdatePACFromGFWListError += controller_UpdatePACFromGFWListError;
            controller.ShowConfigFormEvent += Config_Click;
            controller.ShowSubscribeWindowEvent += Controller_ShowSubscribeWindowEvent;

            _notifyIcon = new TaskbarIcon();
            UpdateTrayIcon();
            _notifyIcon.Visibility = Visibility.Visible;
            _notifyIcon.ContextMenu = _contextMenu;

            _notifyIcon.TrayLeftMouseUp += notifyIcon_TrayLeftMouseUp;
            _notifyIcon.TrayMiddleMouseUp += notifyIcon_TrayMiddleMouseUp;
            _notifyIcon.TrayBalloonTipClicked += notifyIcon_TrayBalloonTipClicked;

            updateChecker = new HttpRequest.UpdateChecker();
            updateChecker.NewVersionFound += updateChecker_NewVersionFound;
            updateChecker.NewVersionNotFound += updateChecker_NewVersionNotFound;
            updateChecker.NewVersionFoundFailed += UpdateChecker_NewVersionFoundFailed;

            Global.UpdateNodeChecker = new UpdateNode();
            Global.UpdateNodeChecker.NewFreeNodeFound += UpdateNodeCheckerNewNodeFound;

            Global.UpdateSubscribeManager = new UpdateSubscribeManager();

            LoadCurrentConfiguration();

            timerDelayCheckUpdate = new System.Timers.Timer(1000.0 * 10);
            timerDelayCheckUpdate.Elapsed += timer_Elapsed;
            timerDelayCheckUpdate.Start();
        }

        private void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            timerDelayCheckUpdate.Interval = 1000.0 * 60 * 60 * 1;// 1 hour

            var cfg = Global.GuiConfig;
            if (cfg.AutoCheckUpdate)
            {
                updateChecker.Check(cfg, false);
            }

            Global.UpdateSubscribeManager.CreateTask(cfg, Global.UpdateNodeChecker, false);
        }

        private static void ControllerError(object sender, ErrorEventArgs e)
        {
            MessageBox.Show(e.GetException().ToString(), string.Format(I18NUtil.GetAppStringValue(@"ControllerError"), e.GetException().Message));
        }

        private void UpdateTrayIcon()
        {
            var config = Global.GuiConfig;
            var enabled = config.SysProxyMode is not ProxyMode.NoModify and not ProxyMode.Direct;
            var global = config.SysProxyMode == ProxyMode.Global;
            var random = config.Random;

            var colorMask = ViewUtils.SelectColorMask(enabled, global);
            var icon = ViewUtils.ChangeBitmapColor(Resources.ss128, colorMask, random);
            var size = ViewUtils.GetIconSize();
            var newIcon = Icon.FromHandle(ViewUtils.ResizeBitmap(icon, size.Width, size.Height).GetHicon());

            if (_notifyIcon.Icon != null)
            {
                ViewUtils.DestroyIcon(_notifyIcon.Icon.Handle);
            }

            _notifyIcon.Icon = newIcon;

            string strServer = null;
            var line3 = string.Empty;
            var line4 = string.Empty;
            if (random)
            {
                strServer = $@"{I18NUtil.GetAppStringValue(@"LoadBalance")}{I18NUtil.GetAppStringValue(@"Colon")}{I18NUtil.GetAppStringValue(config.BalanceType.ToString())}";
                if (config.RandomInGroup)
                {
                    line3 = $@"{I18NUtil.GetAppStringValue(@"BalanceInGroup")}{Environment.NewLine}";
                }

                if (config.AutoBan)
                {
                    line4 = $@"{I18NUtil.GetAppStringValue(@"AutoBan")}{Environment.NewLine}";
                }
            }
            else
            {
                if (config.Index >= 0 && config.Index < config.Configs.Count)
                {
                    var groupName = config.Configs[config.Index].Group;
                    var serverName = config.Configs[config.Index].Remarks;
                    if (string.IsNullOrWhiteSpace(groupName))
                    {
                        strServer = string.IsNullOrWhiteSpace(serverName) ? null : serverName;
                    }
                    else if (string.IsNullOrWhiteSpace(serverName))
                    {
                        strServer = $@"{groupName}";
                    }
                    else
                    {
                        strServer = $@"{groupName}{I18NUtil.GetAppStringValue(@"Colon")}{serverName}";
                    }
                }
            }

            string line1;
            switch (config.SysProxyMode)
            {
                case ProxyMode.NoModify:
                {
                    line1 = $@"{I18NUtil.GetAppStringValue(@"NoProxy")}{Environment.NewLine}";
                    break;
                }
                case ProxyMode.Direct:
                {
                    line1 = $@"{I18NUtil.GetAppStringValue(@"DisableProxy")}{Environment.NewLine}";
                    break;
                }
                case ProxyMode.Pac:
                {
                    line1 = $@"{I18NUtil.GetAppStringValue(@"PacProxy")}{Environment.NewLine}";
                    break;
                }
                case ProxyMode.Global:
                {
                    line1 = $@"{I18NUtil.GetAppStringValue(@"GlobalProxy")}{Environment.NewLine}";
                    break;
                }
                default:
                {
                    line1 = null;
                    break;
                }
            }
            var line2 = string.IsNullOrWhiteSpace(strServer) ? null : $@"{strServer}{Environment.NewLine}";
            var line5 = string.Format(I18NUtil.GetAppStringValue(@"RunningPort"), config.LocalPort); // this feedback is very important because they need to know Shadowsocks is running

            var text = $@"{line1}{line2}{line3}{line4}{line5}";
            _notifyIcon.ToolTipText = text;
        }

        private void LoadMenu()
        {
            if (Application.Current.FindResource(@"SysTrayMenu") is ContextMenu menu)
            {
                _contextMenu = menu;
            }

            I18NUtil.SetLanguage(_contextMenu);
            foreach (var obj in _contextMenu.Items)
            {
                if (obj is MenuItem menuItem)
                {
                    switch (menuItem.Name)
                    {
                        case @"Mode":
                        {
                            enableItem = (MenuItem)menuItem.Items[0];
                            PACModeItem = (MenuItem)menuItem.Items[1];
                            globalModeItem = (MenuItem)menuItem.Items[2];
                            noModifyItem = (MenuItem)menuItem.Items[4];
                            enableItem.Click += EnableItem_Click;
                            PACModeItem.Click += PACModeItem_Click;
                            globalModeItem.Click += GlobalModeItem_Click;
                            noModifyItem.Click += NoModifyItem_Click;
                            break;
                        }
                        case @"ProxySetting":
                        {
                            var pacMenuItem = (MenuItem)menuItem.Items[0];
                            var proxyMenuItem = (MenuItem)menuItem.Items[1];
                            ((MenuItem)menuItem.Items[3]).Click += CopyPacUrlItem_Click;
                            ((MenuItem)menuItem.Items[4]).Click += EditPACFileItem_Click;
                            ((MenuItem)menuItem.Items[5]).Click += EditUserRuleFileForGFWListItem_Click;

                            ((MenuItem)pacMenuItem.Items[0]).Click += UpdatePACFromLanIPListItem_Click;

                            ((MenuItem)pacMenuItem.Items[2]).Click += UpdatePACFromCNWhiteListItem_Click;
                            ((MenuItem)pacMenuItem.Items[3]).Click += UpdatePACFromCnWhiteListIpItem_Click;
                            ((MenuItem)pacMenuItem.Items[4]).Click += UpdatePACFromChnIpItem_Click;
                            ((MenuItem)pacMenuItem.Items[5]).Click += UpdatePACFromGFWListItem_Click;

                            ((MenuItem)pacMenuItem.Items[7]).Click += UpdatePACFromCNOnlyListItem_Click;

                            ruleBypassLan = (MenuItem)proxyMenuItem.Items[0];
                            ruleBypassChina = (MenuItem)proxyMenuItem.Items[1];
                            ruleBypassNotChina = (MenuItem)proxyMenuItem.Items[2];
                            ruleUser = (MenuItem)proxyMenuItem.Items[3];
                            ruleDisableBypass = (MenuItem)proxyMenuItem.Items[5];

                            ruleBypassLan.Click += RuleBypassLanItem_Click;
                            ruleBypassChina.Click += RuleBypassChinaItem_Click;
                            ruleBypassNotChina.Click += RuleBypassNotChinaItem_Click;
                            ruleUser.Click += RuleUserItem_Click;
                            ruleDisableBypass.Click += RuleBypassDisableItem_Click;
                            break;
                        }
                        case @"Servers":
                        {
                            ServersItem = menuItem;
                            SeparatorItem = (Separator)menuItem.Items[0];
                            SelectRandomItem = (MenuItem)menuItem.Items[7];
                            sameHostForSameTargetItem = (MenuItem)menuItem.Items[8];

                            ((MenuItem)menuItem.Items[1]).Click += Config_Click;
                            ((MenuItem)menuItem.Items[3]).Click += ScanQRCodeItem_Click;
                            ((MenuItem)menuItem.Items[4]).Click += ImportAddressFromClipboard_Click;
                            ((MenuItem)menuItem.Items[5]).Click += Import_Click;
                            SelectRandomItem.Click += SelectRandomItem_Click;
                            sameHostForSameTargetItem.Click += SelectSameHostForSameTargetItem_Click;
                            ((MenuItem)menuItem.Items[10]).Click += ShowServerLogItem_Click;
                            ((MenuItem)menuItem.Items[11]).Click += DisconnectCurrent_Click;
                            break;
                        }
                        case @"ServersSubscribe":
                        {
                            ((MenuItem)menuItem.Items[0]).Click += SubscribeSetting_Click;
                            ((MenuItem)menuItem.Items[1]).Click += CheckNodeUpdate_Click;
                            break;
                        }
                        case @"ShowLogs":
                        {
                            menuItem.Click += ShowLogItem_Click;
                            break;
                        }
                        case @"More":
                        {
                            _moreMenu = menuItem;
                            ((MenuItem)_moreMenu.Items[0]).Click += Setting_Click;
                            ((MenuItem)_moreMenu.Items[1]).Click += DnsSetting_Click;
                            ((MenuItem)_moreMenu.Items[2]).Click += ShowPortMapItem_Click;
                            ((MenuItem)_moreMenu.Items[3]).Click += ShowUrlFromQrCode;
                            ((MenuItem)_moreMenu.Items[5]).Click += OpenWiki_Click;
                            ((MenuItem)_moreMenu.Items[6]).Click += FeedbackItem_Click;

                            _updateMenu = (MenuItem)_moreMenu.Items[8];

                            ((MenuItem)_updateMenu.Items[0]).Click += CheckUpdate_Click;
                            UpdateItem = (MenuItem)_updateMenu.Items[1];
                            AutoCheckUpdateItem = (MenuItem)_updateMenu.Items[3];
                            AllowPreReleaseItem = (MenuItem)_updateMenu.Items[4];
                            UpdateItem.Click += UpdateItem_Clicked;
                            AutoCheckUpdateItem.Click += AutoCheckUpdateItem_Click;
                            AllowPreReleaseItem.Click += AllowPreRelease_Click;
                            break;
                        }
                        case @"Quit":
                        {
                            menuItem.Click += Quit_Click;
                            break;
                        }
                    }
                }
            }
        }

        private void controller_ConfigChanged(object sender, EventArgs e)
        {
            LoadCurrentConfiguration();
            UpdateTrayIcon();
        }

        private void controller_FileReadyToOpen(object sender, MainController.PathEventArgs e)
        {
            Utils.OpenURL(e.Path);
        }

        private void controller_UpdatePACFromGFWListError(object sender, ErrorEventArgs e)
        {
            _notifyIcon.ShowBalloonTip(I18NUtil.GetAppStringValue(@"UpdatePacFailed"), e.GetException().Message, BalloonIcon.Error);
            Logging.LogUsefulException(e.GetException());
        }

        private void controller_UpdatePACFromGFWListCompleted(object sender, GfwListUpdater.ResultEventArgs e)
        {
            var result = e.Success ?
                    e.PacType == PacType.GfwList ?
                    I18NUtil.GetAppStringValue(@"GfwListPacUpdated") : I18NUtil.GetAppStringValue(@"PacUpdated")
                : I18NUtil.GetAppStringValue(@"GfwListPacNotFound");
            _notifyIcon.ShowBalloonTip(HttpRequest.UpdateChecker.Name, result, BalloonIcon.Info);
        }

        private void UpdateNodeCheckerNewNodeFound(object sender, EventArgs e)
        {
            if (configFrom_open)
            {
                eventList.Add(new EventParams(sender, e));
                return;
            }
            string lastGroup = null;
            var count = 0;
            if (!string.IsNullOrWhiteSpace(Global.UpdateNodeChecker.FreeNodeResult))
            {
                Global.UpdateNodeChecker.FreeNodeResult = Global.UpdateNodeChecker.FreeNodeResult.TrimEnd('\r', '\n', ' ');
                var config = Global.GuiConfig;
                var selectedServer = config.Configs.ElementAtOrDefault(config.Index);
                try
                {
                    Global.UpdateNodeChecker.FreeNodeResult = Base64.DecodeBase64(Global.UpdateNodeChecker.FreeNodeResult);
                }
                catch
                {
                    Global.UpdateNodeChecker.FreeNodeResult = string.Empty;
                }
                var urls = Global.UpdateNodeChecker.FreeNodeResult.GetLines().ToList();
                urls.RemoveAll(url => !url.StartsWith(@"ssr://"));
                if (urls.Count > 0)
                {
                    lastGroup = Global.UpdateSubscribeManager.CurrentServerSubscribe.OriginTag;
                    if (string.IsNullOrEmpty(lastGroup))
                    {
                        foreach (var url in urls)
                        {
                            try // try get group name
                            {
                                var server = new Server(url, null);
                                if (!string.IsNullOrEmpty(server.Group))
                                {
                                    if (config.ServerSubscribes.Any(subscribe => subscribe.Tag == server.Group))
                                    {
                                        continue;
                                    }

                                    var serverSubscribe = config.ServerSubscribes.Find(sub =>
                                    sub.Url == Global.UpdateSubscribeManager.CurrentServerSubscribe.Url
                                    && string.IsNullOrEmpty(sub.OriginTag));

                                    if (serverSubscribe != null)
                                    {
                                        lastGroup = serverSubscribe.Tag = server.Group;
                                    }

                                    break;
                                }
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(lastGroup))
                    {
                        lastGroup = Global.UpdateSubscribeManager.CurrentServerSubscribe.UrlMd5;
                    }

                    //Find old servers
                    var firstInsertIndex = config.Configs.Count;
                    var oldServers = config.Configs.FindAll(server => server.SubTag == lastGroup);
                    var index = config.Configs.FindIndex(server => server.SubTag == lastGroup);
                    if (index >= 0)
                    {
                        firstInsertIndex = index;
                    }

                    //Find new servers
                    var newServers = new List<Server>();
                    foreach (var url in urls)
                    {
                        try
                        {
                            var server = new Server(url, lastGroup) { Index = firstInsertIndex++ };
                            newServers.Add(server);
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    //Group name is not empty
                    foreach (var newServer in newServers.Where(newServer => string.IsNullOrEmpty(newServer.Group)))
                    {
                        newServer.Group = lastGroup;
                    }

                    count = newServers.Count;

                    var removeServers = oldServers.Except(newServers);
                    var addServers = newServers.Except(oldServers);

                    //Remove servers
                    foreach (var server in removeServers)
                    {
                        server.Connections.CloseAll();
                        config.Configs.Remove(server);
                    }

                    //Add servers
                    foreach (var server in addServers)
                    {
                        if (server.Index > config.Configs.Count)
                        {
                            server.Index = config.Configs.Count;
                        }
                        config.Configs.Insert(server.Index, server);
                    }

                    //Set SelectedServer
                    var selectedIndex = -1;
                    if (selectedServer is not null)
                    {
                        selectedIndex = config.Configs.FindIndex(server => server.Id == selectedServer.Id);

                        if (selectedIndex < 0)
                        {
                            selectedIndex = config.Configs.FindIndex(server =>
                                server.SubTag == selectedServer.SubTag && server.IsMatchServer(selectedServer)
                            );
                        }

                        if (selectedIndex < 0)
                        {
                            selectedIndex = config.Configs.FindIndex(server =>
                                server.SubTag == selectedServer.SubTag
                                && server.Group == selectedServer.Group
                                && server.Remarks == selectedServer.Remarks
                            );
                        }

                        if (selectedIndex < 0)
                        {
                            selectedIndex = config.Configs.FindIndex(server =>
                                server.SubTag == selectedServer.SubTag
                                && server.Group == selectedServer.Group
                            );
                        }

                        if (selectedIndex < 0)
                        {
                            selectedIndex = config.Configs.FindIndex(server => server.SubTag == selectedServer.SubTag);
                        }
                    }

                    config.Index = selectedIndex < 0 ? default : selectedIndex;

                    //If Update Success
                    if (count > 0)
                    {
                        foreach (var serverSubscribe in config.ServerSubscribes.Where(serverSubscribe => serverSubscribe.Url == Global.UpdateNodeChecker.SubscribeTask.Url))
                        {
                            serverSubscribe.LastUpdateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        }

                        var defaultServer = new Server();
                        config.Configs.RemoveAll(server => server.IsMatchServer(defaultServer));
                    }
                    controller.SaveServersConfig(config, true);
                }
            }

            if (count > 0)
            {
                if (Global.UpdateNodeChecker.Notify)
                {
                    _notifyIcon.ShowBalloonTip(I18NUtil.GetAppStringValue(@"Success"), string.Format(I18NUtil.GetAppStringValue(@"UpdateSubscribeSuccess"), lastGroup), BalloonIcon.Info);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(lastGroup))
                {
                    lastGroup = Global.UpdateNodeChecker.SubscribeTask.Tag;
                }

                if (Global.UpdateNodeChecker.Notify)
                {
                    _notifyIcon.ShowBalloonTip(I18NUtil.GetAppStringValue(@"Error"), string.Format(I18NUtil.GetAppStringValue(@"UpdateSubscribeFailure"), lastGroup), BalloonIcon.Info);
                }
            }

            Global.UpdateSubscribeManager.Next();
        }

        private void updateChecker_NewVersionFound(object sender, EventArgs e)
        {
            Application.Current.Dispatcher?.InvokeAsync(() =>
            {
                if (updateChecker.Found)
                {
                    if (UpdateItem.Visibility != Visibility.Visible)
                    {
                        _notifyIcon.ShowBalloonTip(
                                string.Format(I18NUtil.GetAppStringValue(@"NewVersionFound"),
                                        HttpRequest.UpdateChecker.Name, updateChecker.LatestVersionNumber),
                                I18NUtil.GetAppStringValue(@"ClickMenuToDownload"), BalloonIcon.Info);
                    }
                    _moreMenu.Icon = CreateSelectedIcon();
                    _updateMenu.Icon = CreateSelectedIcon();
                    UpdateItem.Visibility = Visibility.Visible;
                    UpdateItem.Header = string.Format(I18NUtil.GetAppStringValue(@"NewVersionAvailable"),
                            HttpRequest.UpdateChecker.Name, updateChecker.LatestVersionNumber);
                }
            });
        }

        private void updateChecker_NewVersionNotFound(object sender, EventArgs e)
        {
            _notifyIcon.ShowBalloonTip($@"{HttpRequest.UpdateChecker.Name} {HttpRequest.UpdateChecker.FullVersion}",
            $@"{I18NUtil.GetAppStringValue(@"NewVersionNotFound")}{Environment.NewLine}{HttpRequest.UpdateChecker.Version}≥{updateChecker.LatestVersionNumber}",
            BalloonIcon.Info);
        }

        private void UpdateChecker_NewVersionFoundFailed(object sender, EventArgs e)
        {
            _notifyIcon.ShowBalloonTip($@"{HttpRequest.UpdateChecker.Name} {HttpRequest.UpdateChecker.FullVersion}", I18NUtil.GetAppStringValue(@"NewVersionFoundFailed"), BalloonIcon.Info);
        }

        private void UpdateItem_Clicked(object sender, RoutedEventArgs e)
        {
            Utils.OpenURL(updateChecker.LatestVersionUrl);
            _moreMenu.Icon = null;
            _updateMenu.Icon = null;
            UpdateItem.Visibility = Visibility.Collapsed;
            updateChecker.Found = false;
        }

        private void notifyIcon_TrayBalloonTipClicked(object sender, RoutedEventArgs e)
        {
            var url = updateChecker.LatestVersionUrl;
            if (!string.IsNullOrWhiteSpace(url))
            {
                Utils.OpenURL(url);
            }
        }

        private void UpdateSysProxyMode(Configuration config)
        {
            noModifyItem.IsChecked = config.SysProxyMode == ProxyMode.NoModify;
            enableItem.IsChecked = config.SysProxyMode == ProxyMode.Direct;
            PACModeItem.IsChecked = config.SysProxyMode == ProxyMode.Pac;
            globalModeItem.IsChecked = config.SysProxyMode == ProxyMode.Global;
        }

        private void UpdateProxyRule(Configuration config)
        {
            ruleDisableBypass.IsChecked = config.ProxyRuleMode == ProxyRuleMode.Disable;
            ruleBypassLan.IsChecked = config.ProxyRuleMode == ProxyRuleMode.BypassLan;
            ruleBypassChina.IsChecked = config.ProxyRuleMode == ProxyRuleMode.BypassLanAndChina;
            ruleBypassNotChina.IsChecked = config.ProxyRuleMode == ProxyRuleMode.BypassLanAndNotChina;
            ruleUser.IsChecked = config.ProxyRuleMode == ProxyRuleMode.UserCustom;
        }

        private void LoadCurrentConfiguration()
        {
            var config = Global.GuiConfig;
            UpdateServersMenu();
            UpdateSysProxyMode(config);

            UpdateProxyRule(config);

            SelectRandomItem.IsChecked = config.Random;
            sameHostForSameTargetItem.IsChecked = config.SameHostForSameTarget;
            AutoCheckUpdateItem.IsChecked = config.AutoCheckUpdate;
            AllowPreReleaseItem.IsChecked = config.IsPreRelease;
        }

        private static Grid CreateSelectedIcon()
        {
            var icon = new Grid();
            icon.Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = Brushes.Black
            });
            return icon;
        }

        private void UpdateServersMenu()
        {
            var items = ServersItem.Items;
            while (!Equals(items[0], SeparatorItem))
            {
                items.RemoveAt(0);
            }

            var configuration = Global.GuiConfig;
            for (var i = 0; i < configuration.Configs.Count;)
            {
                configuration.Configs[i].Index = ++i;
            }
            var sub = new List<MenuItem>();
            var subTags = new HashSet<string>(configuration.Configs.Select(server => server.SubTag));
            foreach (var subTag in subTags)
            {
                var isSelected = false;
                var subItem = new MenuItem
                {
                    Header = string.IsNullOrEmpty(subTag) ? I18NUtil.GetAppStringValue(@"EmptySubtag") : subTag
                };
                var servers = configuration.Configs.Where(server => server.SubTag == subTag).ToArray();
                var groups = new HashSet<string>(servers.Select(server => server.Group));
                foreach (var group in groups)
                {
                    var groupItem = new MenuItem
                    {
                        Header = string.IsNullOrEmpty(group) ? I18NUtil.GetAppStringValue(@"EmptyGroup") : group
                    };
                    var subServers = servers.Where(server => server.Group == group);
                    foreach (var server in subServers)
                    {
                        var item = new MenuItem
                        {
                            Header = server.FriendlyName,
                            Tag = server.Index - 1
                        };
                        item.Click += AServerItem_Click;
                        if (configuration.Index == Convert.ToInt32(item.Tag))
                        {
                            item.IsChecked = true;
                            isSelected = true;
                        }
                        groupItem.Items.Add(item);
                    }
                    if (groups.Count > 1)
                    {
                        subItem.Items.Add(groupItem);
                        if (isSelected)
                        {
                            groupItem.Icon = CreateSelectedIcon();
                            subItem.Icon = CreateSelectedIcon();
                            isSelected = false;
                        }
                    }
                    else
                    {
                        groupItem.Header = subItem.Header;
                        sub.Add(groupItem);
                        if (isSelected)
                        {
                            groupItem.Icon = CreateSelectedIcon();
                        }
                    }
                }
                if (groups.Count > 1)
                {
                    sub.Add(subItem);
                }
            }
            var index = 0;
            foreach (var menuItem in sub)
            {
                items.Insert(index++, menuItem);
            }
        }

        private void ShowConfigForm(bool addNode)
        {
            if (_serverConfigWindow != null)
            {
                _serverConfigWindow.Activate();
                _serverConfigWindow.UpdateLayout();
                if (_serverConfigWindow.WindowState == WindowState.Minimized)
                {
                    _serverConfigWindow.WindowState = WindowState.Normal;
                }
                if (addNode)
                {
                    var cfg = Global.GuiConfig;
                    _serverConfigWindow.MoveToSelectedItem(cfg.Index + 1);
                }
            }
            else
            {
                configFrom_open = true;
                _serverConfigWindow = new ServerConfigWindow(controller, addNode ? -1 : -2);
                _serverConfigWindow.Show();
                _serverConfigWindow.Activate();
                _serverConfigWindow.BringToFront();
                _serverConfigWindow.Closed += ServerConfigWindowClosed;
            }
        }

        private void ServerConfigWindowClosed(object sender, EventArgs e)
        {
            _serverConfigWindow = null;
            configFrom_open = false;
            if (eventList.Count > 0)
            {
                foreach (var p in eventList)
                {
                    UpdateNodeCheckerNewNodeFound(p.sender, p.e);
                }

                eventList.Clear();
            }
        }

        private void ShowConfigForm(int index)
        {
            if (_serverConfigWindow != null)
            {
                _serverConfigWindow.Activate();
                _serverConfigWindow.UpdateLayout();
                if (_serverConfigWindow.WindowState == WindowState.Minimized)
                {
                    _serverConfigWindow.WindowState = WindowState.Normal;
                }
                _serverConfigWindow.Topmost = true;
                _serverConfigWindow.MoveToSelectedItem(index);
            }
            else
            {
                configFrom_open = true;
                _serverConfigWindow = new ServerConfigWindow(controller, index);
                _serverConfigWindow.Show();
                _serverConfigWindow.Activate();
                _serverConfigWindow.Topmost = true;
                _serverConfigWindow.Closed += ServerConfigWindowClosed;
            }
        }

        private void ShowSettingForm()
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
            }
            else
            {
                _settingsWindow = new SettingsWindow(controller);
                _settingsWindow.Show();
                _settingsWindow.Activate();
                _settingsWindow.BringToFront();
                _settingsWindow.Closed += (_, _) =>
                {
                    _settingsWindow = null;
                };
            }
        }

        private void ShowDnsSettingWindow()
        {
            if (_dnsSettingsWindow != null)
            {
                _dnsSettingsWindow.Activate();
            }
            else
            {
                _dnsSettingsWindow = new DnsSettingWindow();
                _dnsSettingsWindow.Show();
                _dnsSettingsWindow.Activate();
                _dnsSettingsWindow.BringToFront();
                _dnsSettingsWindow.Closed += (o, args) =>
                {
                    _dnsSettingsWindow = null;
                };
            }
        }

        private void ShowPortMapForm()
        {
            if (_portMapWindow != null)
            {
                _portMapWindow.Activate();
                _portMapWindow.UpdateLayout();
                if (_portMapWindow.WindowState == WindowState.Minimized)
                {
                    _portMapWindow.WindowState = WindowState.Normal;
                }
            }
            else
            {
                _portMapWindow = new PortSettingsWindow(controller);
                _portMapWindow.Show();
                _portMapWindow.Activate();
                _portMapWindow.BringToFront();
                _portMapWindow.Closed += (o, e) =>
                {
                    _portMapWindow = null;
                };
            }
        }

        private void ShowServerLogForm()
        {
            if (_serverLogWindow != null)
            {
                _serverLogWindow.Activate();
                _serverLogWindow.UpdateLayout();
                if (_serverLogWindow.WindowState == WindowState.Minimized)
                {
                    _serverLogWindow.WindowState = WindowState.Normal;
                }
            }
            else
            {
                _serverLogWindow = new ServerLogWindow(controller, _serverLogWindowStatus);
                _serverLogWindow.Show();
                _serverLogWindow.Activate();
                _serverLogWindow.BringToFront();
                _serverLogWindow.Closed += (o, e) =>
                {
                    _serverLogWindowStatus = new WindowStatus(_serverLogWindow);
                    _serverLogWindow = null;
                };
            }
        }

        private void ShowGlobalLogWindow()
        {
            if (_logWindow != null)
            {
                _logWindow.Activate();
                _logWindow.UpdateLayout();
                if (_logWindow.WindowState == WindowState.Minimized)
                {
                    _logWindow.WindowState = WindowState.Normal;
                }
            }
            else
            {
                _logWindow = new LogWindow();
                _logWindow.Show();
                _logWindow.Activate();
                _logWindow.BringToFront();
                _logWindow.Closed += (sender, args) =>
                {
                    _logWindow = null;
                };
            }
        }

        private void ShowSubscribeSettingForm()
        {
            if (_subScribeWindow != null)
            {
                _subScribeWindow.Activate();
                _subScribeWindow.UpdateLayout();
                if (_subScribeWindow.WindowState == WindowState.Minimized)
                {
                    _subScribeWindow.WindowState = WindowState.Normal;
                }
            }
            else
            {
                _subScribeWindow = new SubscribeWindow(controller);
                _subScribeWindow.Show();
                _subScribeWindow.Activate();
                _subScribeWindow.BringToFront();
                _subScribeWindow.Closed += (sender, args) =>
                {
                    _subScribeWindow = null;
                };
            }
        }

        private void Config_Click(object sender, EventArgs e)
        {
            if (sender is int i)
            {
                ShowConfigForm(i);
            }
            else
            {
                ShowConfigForm(false);
            }
        }

        private void Controller_ShowSubscribeWindowEvent(object sender, EventArgs e)
        {
            ShowSubscribeSettingForm();
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                var dlg = new OpenFileDialog
                {
                    InitialDirectory = Directory.GetCurrentDirectory()
                };
                if (dlg.ShowDialog() == true)
                {
                    var name = dlg.FileName;
                    var cfg = Global.LoadFile(name);
                    if (cfg.IsDefaultConfig())
                    {
                        MessageBox.Show(I18NUtil.GetAppStringValue(@"ImportConfigFailed"), HttpRequest.UpdateChecker.Name);
                    }
                    else
                    {
                        controller.MergeConfiguration(cfg);
                    }
                }
            });
        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingForm();
        }

        private void DnsSetting_Click(object sender, RoutedEventArgs e)
        {
            ShowDnsSettingWindow();
        }

        public void Quit_Click(object sender, EventArgs e)
        {
            controller.Stop();
            if (_serverConfigWindow != null)
            {
                _serverConfigWindow.Close();
                _serverConfigWindow = null;
            }
            if (_serverLogWindow != null)
            {
                _serverLogWindow.Close();
                _serverLogWindow = null;
            }
            if (timerDelayCheckUpdate != null)
            {
                timerDelayCheckUpdate.Elapsed -= timer_Elapsed;
                timerDelayCheckUpdate.Stop();
                timerDelayCheckUpdate = null;
            }
            if (_notifyIcon.Icon != null)
            {
                ViewUtils.DestroyIcon(_notifyIcon.Icon.Handle);
            }
            _notifyIcon.Dispose();
            Application.Current.Shutdown();
        }

        private static void OpenWiki_Click(object sender, RoutedEventArgs e)
        {
            Utils.OpenURL(@"https://github.com/HMBSbige/ShadowsocksR-Windows/wiki");
        }

        private static void FeedbackItem_Click(object sender, RoutedEventArgs e)
        {
            Utils.OpenURL(@"https://github.com/HMBSbige/ShadowsocksR-Windows/issues/new/choose");
        }

        private void notifyIcon_TrayLeftMouseUp(object sender, RoutedEventArgs e)
        {
            var key = Keyboard.IsKeyDown(Key.LeftShift) ? 1 : 0;
            key |= Keyboard.IsKeyDown(Key.RightShift) ? 1 : 0;
            key |= Keyboard.IsKeyDown(Key.LeftCtrl) ? 2 : 0;
            key |= Keyboard.IsKeyDown(Key.RightCtrl) ? 2 : 0;
            key |= Keyboard.IsKeyDown(Key.LeftAlt) ? 4 : 0;
            switch (key)
            {
                case 1:
                    ShowSettingForm();
                    break;
                case 2:
                    ShowServerLogForm();
                    break;
                case 3:
                    ShowSubscribeSettingForm();
                    break;
                case 4:
                    ShowPortMapForm();
                    break;
                case 6:
                    ShowDnsSettingWindow();
                    break;
                default:
                    ShowConfigForm(false);
                    break;
            }
        }

        private void notifyIcon_TrayMiddleMouseUp(object sender, RoutedEventArgs e)
        {
            ShowServerLogForm();
        }

        private void NoModifyItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { controller.ToggleMode(ProxyMode.NoModify); });
        }

        private void EnableItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { controller.ToggleMode(ProxyMode.Direct); });
        }

        private void GlobalModeItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { controller.ToggleMode(ProxyMode.Global); });
        }

        private void PACModeItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { controller.ToggleMode(ProxyMode.Pac); });
        }

        private void RuleBypassLanItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { controller.ToggleRuleMode(ProxyRuleMode.BypassLan); });
        }

        private void RuleBypassChinaItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { controller.ToggleRuleMode(ProxyRuleMode.BypassLanAndChina); });
        }

        private void RuleBypassNotChinaItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { controller.ToggleRuleMode(ProxyRuleMode.BypassLanAndNotChina); });
        }

        private void RuleUserItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { controller.ToggleRuleMode(ProxyRuleMode.UserCustom); });
        }

        private void RuleBypassDisableItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { controller.ToggleRuleMode(ProxyRuleMode.Disable); });
        }

        private void SelectRandomItem_Click(object sender, RoutedEventArgs e)
        {
            SelectRandomItem.IsChecked = !SelectRandomItem.IsChecked;
            if (SelectRandomItem.IsChecked)
            {
                Task.Run(() => { controller.ToggleSelectRandom(true); });
            }
            else
            {
                Task.Run(() => { controller.ToggleSelectRandom(false); });
            }
        }

        private void AutoCheckUpdateItem_Click(object sender, RoutedEventArgs e)
        {
            AutoCheckUpdateItem.IsChecked = !AutoCheckUpdateItem.IsChecked;
            controller.ToggleSelectAutoCheckUpdate(AutoCheckUpdateItem.IsChecked);
        }

        private void AllowPreRelease_Click(object sender, RoutedEventArgs e)
        {
            AllowPreReleaseItem.IsChecked = !AllowPreReleaseItem.IsChecked;
            controller.ToggleSelectAllowPreRelease(AllowPreReleaseItem.IsChecked);
        }

        private void SelectSameHostForSameTargetItem_Click(object sender, RoutedEventArgs e)
        {
            sameHostForSameTargetItem.IsChecked = !sameHostForSameTargetItem.IsChecked;
            if (sameHostForSameTargetItem.IsChecked)
            {
                Task.Run(() => { controller.ToggleSameHostForSameTargetRandom(true); });
            }
            else
            {
                Task.Run(() => { controller.ToggleSameHostForSameTargetRandom(false); });
            }
        }

        private void CopyPacUrlItem_Click(object sender, RoutedEventArgs e)
        {
            controller.CopyPacUrl();
        }

        private void EditPACFileItem_Click(object sender, RoutedEventArgs e)
        {
            controller.TouchPACFile();
        }

        private void UpdatePACFromGFWListItem_Click(object sender, RoutedEventArgs e)
        {
            controller.UpdatePACFromGFWList();
        }

        private void UpdatePACFromLanIPListItem_Click(object sender, RoutedEventArgs e)
        {
            controller.UpdatePACFromOnlinePac(@"https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/ss_lanip.pac");
        }

        private void UpdatePACFromCNWhiteListItem_Click(object sender, RoutedEventArgs e)
        {
            //域名白名单
            controller.UpdatePACFromOnlinePac(@"https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/ss_white.pac");
        }

        private void UpdatePACFromCNOnlyListItem_Click(object sender, RoutedEventArgs e)
        {
            //回国
            controller.UpdatePACFromOnlinePac(@"https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/ss_white_r.pac");
        }

        private void UpdatePACFromCnWhiteListIpItem_Click(object sender, RoutedEventArgs e)
        {
            //域名白名单+国内IP
            controller.UpdatePACFromOnlinePac(@"https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/ss_cnall.pac");
        }

        private void UpdatePACFromChnIpItem_Click(object sender, RoutedEventArgs e)
        {
            //国内IP
            controller.UpdatePACFromOnlinePac(@"https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/ss_cnip.pac");
        }

        private void EditUserRuleFileForGFWListItem_Click(object sender, RoutedEventArgs e)
        {
            controller.TouchUserRuleFile();
        }

        private void AServerItem_Click(object sender, EventArgs e)
        {
            var item = (MenuItem)sender;
            var index = (int)item.Tag;
            Task.Run(() =>
            {
                controller.DisconnectAllConnections(true);
                controller.SelectServerIndex(index);
            });
        }

        private void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            updateChecker.Check(Global.GuiConfig, true);
        }

        private void CheckNodeUpdate_Click(object sender, RoutedEventArgs e)
        {
            Global.UpdateSubscribeManager.CreateTask(Global.GuiConfig, Global.UpdateNodeChecker, true);
        }

        private void ShowLogItem_Click(object sender, RoutedEventArgs e)
        {
            ShowGlobalLogWindow();
        }

        private void ShowPortMapItem_Click(object sender, RoutedEventArgs e)
        {
            ShowPortMapForm();
        }

        private void ShowServerLogItem_Click(object sender, RoutedEventArgs e)
        {
            ShowServerLogForm();
        }

        private void SubscribeSetting_Click(object sender, RoutedEventArgs e)
        {
            ShowSubscribeSettingForm();
        }

        private void DisconnectCurrent_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { controller.DisconnectAllConnections(); });
        }

        public void ImportAddress(string text)
        {
            ImportSsrUrl(text);

            ImportSubUrl(text);
        }

        private void ImportSsrUrl(string text)
        {
            if (controller.AddServerBySsUrl(text))
            {
                ShowConfigForm(true);
            }
        }

        private void ImportSubUrl(string text)
        {
            if (controller.AddSubscribeUrl(text))
            {
                ShowSubscribeSettingForm();
            }
        }

        private void ImportAddressFromClipboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var iData = Clipboard.GetDataObject();
                if (iData != null && iData.GetDataPresent(DataFormats.Text))
                {
                    ImportAddress((string)iData.GetData(DataFormats.Text));
                }
            }
            catch
            {
                // ignored
            }
        }

        #region QRCode

        private void ScanQRCodeItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                var w = (int)SystemParameters.VirtualScreenWidth;
                var h = (int)SystemParameters.VirtualScreenHeight;
                var x = (int)SystemParameters.VirtualScreenLeft;
                var y = (int)SystemParameters.VirtualScreenTop;
                var fullImage = new Bitmap(w, h);
                using (var g = Graphics.FromImage(fullImage))
                {
                    g.CopyFromScreen(x, y,
                            0, 0,
                            fullImage.Size,
                            CopyPixelOperation.SourceCopy);
                }

                const int maxTry = 10;
                for (var i = 0; i < maxTry; i++)
                {
                    var marginLeft = (int)((double)fullImage.Width * i / 2.5 / maxTry);
                    var marginTop = (int)((double)fullImage.Height * i / 2.5 / maxTry);
                    var cropRect = new Rectangle(marginLeft, marginTop, fullImage.Width - marginLeft * 2,
                            fullImage.Height - marginTop * 2);
                    var target = new Bitmap(w, h);

                    var imageScale = w / (double)cropRect.Width;
                    using (var g = Graphics.FromImage(target))
                    {
                        g.DrawImage(fullImage,
                        new Rectangle(0, 0, target.Width, target.Height),
                                cropRect,
                                GraphicsUnit.Pixel);
                    }

                    var result = QrCodeUtils.ScanBitmap(target);
                    if (result != null)
                    {
                        var success = controller.AddServerBySsUrl(result.Text);
                        var successSub = controller.AddSubscribeUrl(result.Text);
                        Application.Current.Dispatcher?.InvokeAsync(() =>
                        {
                            var splash = new QRCodeSplashWindow();
                            if (successSub)
                            {
                                splash.Closed += Splash_Closed0;
                            }
                            if (success)
                            {
                                splash.Closed += Splash_Closed;
                            }
                            if (!(successSub || success))
                            {
                                _urlToOpen = result.Text;
                                splash.Closed += Splash_Closed2;
                            }

                            double minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0;
                            foreach (var point in result.ResultPoints)
                            {
                                minX = Math.Min(minX, point.X);
                                minY = Math.Min(minY, point.Y);
                                maxX = Math.Max(maxX, point.X);
                                maxY = Math.Max(maxY, point.Y);
                            }

                            minX /= imageScale;
                            minY /= imageScale;
                            maxX /= imageScale;
                            maxY /= imageScale;
                            // make it 20% larger
                            var margin = (maxX - minX) * 0.20f;
                            minX += -margin + marginLeft;
                            maxX += margin + marginLeft;
                            minY += -margin + marginTop;
                            maxY += margin + marginTop;
                            splash.Left = x;
                            splash.Top = y;
                            splash.TargetRect = new Rectangle((int)minX, (int)minY, (int)maxX - (int)minX,
                                    (int)maxY - (int)minY);
                            splash.Width = fullImage.Width;
                            splash.Height = fullImage.Height;
                            fullImage.Dispose();
                            splash.Show();
                        });
                        return;
                    }
                }

                MessageBox.Show(I18NUtil.GetAppStringValue(@"QrCodeNotFound"));
            });
        }

        private void Splash_Closed(object sender, EventArgs e)
        {
            ShowConfigForm(true);
        }

        private void Splash_Closed0(object sender, EventArgs e)
        {
            ShowSubscribeSettingForm();
        }

        private void ShowUrlFromQrCode()
        {
            var dlg = new ShowTextWindow(_urlToOpen);
            dlg.Show();
            dlg.Activate();
            dlg.BringToFront();
        }

        private void Splash_Closed2(object sender, EventArgs e)
        {
            ShowUrlFromQrCode();
        }

        private void ShowUrlFromQrCode(object sender, RoutedEventArgs e)
        {
            ShowUrlFromQrCode();
        }

        #endregion
    }
}
