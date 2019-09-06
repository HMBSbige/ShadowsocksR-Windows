using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using Shadowsocks.Controller;
using Shadowsocks.Model;
using Shadowsocks.Properties;
using Shadowsocks.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Shadowsocks.View
{
    public class EventParams
    {
        public object sender;
        public EventArgs e;

        public EventParams(object sender, EventArgs e)
        {
            this.sender = sender;
            this.e = e;
        }
    }

    public class MenuViewController
    {
        // yes this is just a menu view controller
        // when config form is closed, it moves away from RAM
        // and it should just do anything related to the config form

        private readonly ShadowsocksController controller;
        private readonly UpdateChecker updateChecker;
        private readonly UpdateFreeNode updateFreeNodeChecker;
        private readonly UpdateSubscribeManager updateSubscribeManager;

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
        private MenuItem UpdateItem;
        private MenuItem AutoCheckUpdateItem;
        private MenuItem AllowPreReleaseItem;
        private ConfigWindow _configWindow;
        private SettingsWindow _settingsWindow;

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
        private readonly List<EventParams> eventList = new List<EventParams>();

        public MenuViewController(ShadowsocksController controller)
        {
            this.controller = controller;

            LoadMenu();

            controller.ToggleModeChanged += controller_ToggleModeChanged;
            controller.ToggleRuleModeChanged += controller_ToggleRuleModeChanged;
            controller.ConfigChanged += controller_ConfigChanged;
            controller.PACFileReadyToOpen += controller_FileReadyToOpen;
            controller.UserRuleFileReadyToOpen += controller_FileReadyToOpen;
            controller.Errored += controller_Errored;
            controller.UpdatePACFromGFWListCompleted += controller_UpdatePACFromGFWListCompleted;
            controller.UpdatePACFromChnDomainsAndIPCompleted += controller_UpdatePACFromChnDomainsAndIPCompleted;
            controller.UpdatePACFromGFWListError += controller_UpdatePACFromGFWListError;
            controller.ShowConfigFormEvent += Config_Click;

            _notifyIcon = new TaskbarIcon();
            UpdateTrayIcon();
            _notifyIcon.Visibility = Visibility.Visible;
            _notifyIcon.ContextMenu = _contextMenu;

            _notifyIcon.TrayLeftMouseUp += notifyIcon_TrayLeftMouseUp;
            _notifyIcon.TrayMiddleMouseUp += notifyIcon_TrayMiddleMouseUp;
            _notifyIcon.TrayBalloonTipClicked += notifyIcon_TrayBalloonTipClicked;

            updateChecker = new UpdateChecker();
            updateChecker.NewVersionFound += updateChecker_NewVersionFound;
            updateChecker.NewVersionNotFound += updateChecker_NewVersionNotFound;
            updateChecker.NewVersionFoundFailed += UpdateChecker_NewVersionFoundFailed;

            updateFreeNodeChecker = new UpdateFreeNode();
            updateFreeNodeChecker.NewFreeNodeFound += updateFreeNodeChecker_NewFreeNodeFound;

            updateSubscribeManager = new UpdateSubscribeManager();

            LoadCurrentConfiguration();

            timerDelayCheckUpdate = new System.Timers.Timer(1000.0 * 10);
            timerDelayCheckUpdate.Elapsed += timer_Elapsed;
            timerDelayCheckUpdate.Start();
        }

        private void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            timerDelayCheckUpdate.Interval = 1000.0 * 60 * 60 * 1;// 1 hours

            var cfg = controller.GetCurrentConfiguration();
            if (cfg.AutoCheckUpdate)
            {
                updateChecker.Check(cfg, false);
            }

            if (cfg.IsDefaultConfig() || cfg.nodeFeedAutoUpdate)
            {
                updateSubscribeManager.CreateTask(cfg, updateFreeNodeChecker, -1, !cfg.IsDefaultConfig(), false);
            }
        }

        private void controller_Errored(object sender, ErrorEventArgs e)
        {
            MessageBox.Show(e.GetException().ToString(), string.Format(I18N.GetString("Shadowsocks Error: {0}"), e.GetException().Message));
        }

        private void UpdateTrayIcon()
        {
            var config = controller.GetCurrentConfiguration();
            var enabled = config.sysProxyMode != (int)ProxyMode.NoModify && config.sysProxyMode != (int)ProxyMode.Direct;
            var global = config.sysProxyMode == (int)ProxyMode.Global;
            var random = config.random;

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
                strServer = $@"{I18N.GetString(@"Load balance")}{I18N.GetString(@": ")}{I18N.GetString(config.balanceAlgorithm)}";
                if (config.randomInGroup)
                {
                    line3 = $@"{I18N.GetString(@"Balance in group")}{Environment.NewLine}";
                }

                if (config.autoBan)
                {
                    line4 = $@"{I18N.GetString(@"AutoBan")}{Environment.NewLine}";
                }
            }
            else
            {
                if (config.index >= 0 && config.index < config.configs.Count)
                {
                    var groupName = config.configs[config.index].Group;
                    var serverName = config.configs[config.index].Remarks;
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
                        strServer = $@"{groupName}{I18N.GetString(@": ")}{serverName}";
                    }
                }
            }
            var line1 = (enabled
                                ? global ? I18N.GetString(@"Global") : I18N.GetString(@"PAC")
                                : I18N.GetString(@"Disable system proxy"))
                                + Environment.NewLine;
            var line2 = string.IsNullOrWhiteSpace(strServer) ? null : $@"{strServer}{Environment.NewLine}";
            var line5 = string.Format(I18N.GetString(@"Running: Port {0}"), config.localPort); // this feedback is very important because they need to know Shadowsocks is running

            var text = $@"{line1}{line2}{line3}{line4}{line5}";
            _notifyIcon.ToolTipText = text;
        }

        private static MenuItem CreateMenuGroup(string text, IEnumerable items)
        {
            var t = new MenuItem
            {
                Header = text,
                BorderThickness = new Thickness(3)
            };
            foreach (var item in items)
            {
                t.Items.Add(item);
            }
            return t;
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
                        case @"PAC":
                        {
                            ((MenuItem)menuItem.Items[0]).Click += UpdatePACFromLanIPListItem_Click;
                            ((MenuItem)menuItem.Items[2]).Click += UpdatePACFromCNWhiteListItem_Click;
                            ((MenuItem)menuItem.Items[3]).Click += UpdatePACFromCNIPListItem_Click;
                            ((MenuItem)menuItem.Items[4]).Click += UpdatePACFromGFWListItem_Click;
                            ((MenuItem)menuItem.Items[6]).Click += UpdatePACFromCNOnlyListItem_Click;
                            ((MenuItem)menuItem.Items[8]).Click += CopyPacUrlItem_Click;
                            ((MenuItem)menuItem.Items[9]).Click += EditPACFileItem_Click;
                            ((MenuItem)menuItem.Items[10]).Click += EditUserRuleFileForGFWListItem_Click;
                            break;
                        }
                        case @"ProxyRule":
                        {
                            ruleBypassLan = (MenuItem)menuItem.Items[0];
                            ruleBypassChina = (MenuItem)menuItem.Items[1];
                            ruleBypassNotChina = (MenuItem)menuItem.Items[2];
                            ruleUser = (MenuItem)menuItem.Items[3];
                            ruleDisableBypass = (MenuItem)menuItem.Items[5];

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
                            SelectRandomItem = (MenuItem)menuItem.Items[4];
                            sameHostForSameTargetItem = (MenuItem)menuItem.Items[5];

                            ((MenuItem)menuItem.Items[1]).Click += Config_Click;
                            ((MenuItem)menuItem.Items[2]).Click += Import_Click;
                            SelectRandomItem.Click += SelectRandomItem_Click;
                            sameHostForSameTargetItem.Click += SelectSameHostForSameTargetItem_Click;
                            ((MenuItem)menuItem.Items[7]).Click += ShowServerLogItem_Click;
                            ((MenuItem)menuItem.Items[8]).Click += DisconnectCurrent_Click;
                            break;
                        }
                        case @"ServersSubscribe":
                        {
                            ((MenuItem)menuItem.Items[0]).Click += SubscribeSetting_Click;
                            ((MenuItem)menuItem.Items[1]).Click += CheckNodeUpdate_Click;
                            ((MenuItem)menuItem.Items[2]).Click += CheckNodeUpdateBypassProxy_Click;
                            break;
                        }
                        case @"GlobalSettings":
                        {
                            menuItem.Click += Setting_Click;
                            break;
                        }
                        case @"PortSettings":
                        {
                            menuItem.Click += ShowPortMapItem_Click;
                            break;
                        }
                        case @"ShowLogs":
                        {
                            menuItem.Click += ShowLogItem_Click;
                            break;
                        }
                        case @"UpdateAvailable":
                        {
                            UpdateItem = menuItem;
                            UpdateItem.Click += UpdateItem_Clicked;
                            break;
                        }
                        case @"ScanQrCode":
                        {
                            menuItem.Click += ScanQRCodeItem_Click;
                            break;
                        }
                        case @"ImportSsrLinksFromClipboard":
                        {
                            menuItem.Click += ImportAddressFromClipboard_Click;
                            break;
                        }
                        case @"Help":
                        {

                            ((MenuItem)menuItem.Items[0]).Click += OpenWiki_Click;
                            ((MenuItem)menuItem.Items[1]).Click += FeedbackItem_Click;
                            ((MenuItem)menuItem.Items[2]).Click += DonateMenuItem_Click;
                            ((MenuItem)menuItem.Items[4]).Click += showURLFromQRCode;
                            ((MenuItem)menuItem.Items[5]).Click += ResetPasswordItem_Click;
                            ((MenuItem)menuItem.Items[8]).Click += AboutItem_Click;

                            var updateMenu = (MenuItem)menuItem.Items[7];
                            ((MenuItem)updateMenu.Items[0]).Click += CheckUpdate_Click;
                            AutoCheckUpdateItem = (MenuItem)updateMenu.Items[2];
                            AllowPreReleaseItem = (MenuItem)updateMenu.Items[3];
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

        private void controller_ToggleModeChanged(object sender, EventArgs e)
        {
            var config = controller.GetCurrentConfiguration();
            UpdateSysProxyMode(config);
        }

        private void controller_ToggleRuleModeChanged(object sender, EventArgs e)
        {
            var config = controller.GetCurrentConfiguration();
            UpdateProxyRule(config);
        }

        private void controller_FileReadyToOpen(object sender, ShadowsocksController.PathEventArgs e)
        {
            Utils.OpenURL(e.Path);
        }

        private void controller_UpdatePACFromGFWListError(object sender, ErrorEventArgs e)
        {
            _notifyIcon.ShowBalloonTip(I18N.GetString(@"Failed to update PAC file"), e.GetException().Message, BalloonIcon.Error);
            Logging.LogUsefulException(e.GetException());
        }

        private void controller_UpdatePACFromGFWListCompleted(object sender, GFWListUpdater.ResultEventArgs e)
        {
            var updater = (GFWListUpdater)sender;
            var result = e.Success ?
                updater.UpdateType < 1 ? I18N.GetString(@"GFWList PAC updated") : I18N.GetString(@"PAC updated")
                : I18N.GetString(@"No updates found. Please report to GFWList if you have problems with it.");
            _notifyIcon.ShowBalloonTip(I18N.GetString(@"ShadowsocksR"), result, BalloonIcon.Info);
        }

        private void controller_UpdatePACFromChnDomainsAndIPCompleted(object sender, ChnDomainsAndIPUpdater.ResultEventArgs e)
        {
            var result = e.Success ? I18N.GetString(@"PAC updated") : I18N.GetString(@"No updates found.");
            _notifyIcon.ShowBalloonTip(I18N.GetString(@"ShadowsocksR"), result, BalloonIcon.Info);
        }

        [SuppressMessage("ReSharper", "LoopVariableIsNeverChangedInsideLoop")]
        private void updateFreeNodeChecker_NewFreeNodeFound(object sender, EventArgs e)
        {
            //TODO
            if (configFrom_open)
            {
                eventList.Add(new EventParams(sender, e));
                return;
            }
            string lastGroup = null;
            var count = 0;
            if (!string.IsNullOrEmpty(updateFreeNodeChecker.FreeNodeResult))
            {
                var urls = new List<string>();
                updateFreeNodeChecker.FreeNodeResult = updateFreeNodeChecker.FreeNodeResult.TrimEnd('\r', '\n', ' ');
                var config = controller.GetCurrentConfiguration();
                Server selected_server = null;
                if (config.index >= 0 && config.index < config.configs.Count)
                {
                    selected_server = config.configs[config.index];
                }
                try
                {
                    updateFreeNodeChecker.FreeNodeResult = Base64.DecodeBase64(updateFreeNodeChecker.FreeNodeResult);
                }
                catch
                {
                    updateFreeNodeChecker.FreeNodeResult = string.Empty;
                }
                var max_node_num = 0;

                var match_maxnum = Regex.Match(updateFreeNodeChecker.FreeNodeResult, "^MAX=([0-9]+)");
                if (match_maxnum.Success)
                {
                    try
                    {
                        max_node_num = Convert.ToInt32(match_maxnum.Groups[1].Value, 10);
                    }
                    catch
                    {
                        // ignored
                    }
                }
                Utils.URL_Split(updateFreeNodeChecker.FreeNodeResult, ref urls);
                for (var i = urls.Count - 1; i >= 0; --i)
                {
                    if (!urls[i].StartsWith("ssr"))
                        urls.RemoveAt(i);
                }
                if (urls.Count > 0)
                {
                    var keep_selected_server = false; // set 'false' if import all nodes
                    if (max_node_num <= 0 || max_node_num >= urls.Count)
                    {
                        urls.Reverse();
                    }
                    else
                    {
                        var r = new Random();
                        Utils.Shuffle(urls, r);
                        urls.RemoveRange(max_node_num, urls.Count - max_node_num);
                        if (!config.IsDefaultConfig())
                            keep_selected_server = true;
                    }
                    string curGroup = null;
                    foreach (var url in urls)
                    {
                        try // try get group name
                        {
                            var server = new Server(url, null);
                            if (!string.IsNullOrEmpty(server.Group))
                            {
                                curGroup = server.Group;
                                break;
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    var subscribeURL = updateSubscribeManager.Url;
                    if (string.IsNullOrEmpty(curGroup))
                    {
                        curGroup = subscribeURL;
                    }
                    foreach (var serverSubscribe in config.serverSubscribes)
                    {
                        if (subscribeURL == serverSubscribe.URL)
                        {
                            lastGroup = serverSubscribe.Group;
                            serverSubscribe.Group = curGroup;
                            break;
                        }
                    }
                    if (string.IsNullOrEmpty(lastGroup))
                    {
                        lastGroup = curGroup;
                    }

                    Debug.Assert(selected_server != null, nameof(selected_server) + " != null");
                    if (keep_selected_server && selected_server.Group == curGroup)
                    {
                        var match = false;
                        foreach (var url in urls)
                        {
                            try
                            {
                                var server = new Server(url, null);
                                if (selected_server.IsMatchServer(server))
                                {
                                    match = true;
                                    break;
                                }
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                        if (!match)
                        {
                            urls.RemoveAt(0);
                            urls.Add(selected_server.SsrLink);
                        }
                    }

                    // import all, find difference
                    {
                        var old_servers = new Dictionary<string, Server>();
                        var old_insert_servers = new Dictionary<string, Server>();
                        if (!string.IsNullOrEmpty(lastGroup))
                        {
                            for (var i = config.configs.Count - 1; i >= 0; --i)
                            {
                                if (lastGroup == config.configs[i].Group)
                                {
                                    old_servers[config.configs[i].Id] = config.configs[i];
                                }
                            }
                        }
                        foreach (var url in urls)
                        {
                            try
                            {
                                var server = new Server(url, curGroup);
                                var match = false;
                                if (!match)
                                {
                                    foreach (var pair in old_insert_servers)
                                    {
                                        if (server.IsMatchServer(pair.Value))
                                        {
                                            match = true;
                                            break;
                                        }
                                    }
                                }
                                old_insert_servers[server.Id] = server;
                                if (!match)
                                {
                                    foreach (var pair in old_servers)
                                    {
                                        if (server.IsMatchServer(pair.Value))
                                        {
                                            match = true;
                                            old_servers.Remove(pair.Key);
                                            pair.Value.CopyServerInfo(server);
                                            ++count;
                                            break;
                                        }
                                    }
                                }
                                if (!match)
                                {
                                    var insert_index = config.configs.Count;
                                    for (var index = config.configs.Count - 1; index >= 0; --index)
                                    {
                                        if (config.configs[index].Group == curGroup)
                                        {
                                            insert_index = index + 1;
                                            break;
                                        }
                                    }
                                    config.configs.Insert(insert_index, server);
                                    ++count;
                                }
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                        foreach (var pair in old_servers)
                        {
                            for (var i = config.configs.Count - 1; i >= 0; --i)
                            {
                                if (config.configs[i].Id == pair.Key)
                                {
                                    config.configs.RemoveAt(i);
                                    break;
                                }
                            }
                        }
                        controller.SaveServersConfig(config);
                    }
                    config = controller.GetCurrentConfiguration();
                    if (selected_server != null)
                    {
                        var match = false;
                        for (var i = config.configs.Count - 1; i >= 0; --i)
                        {
                            if (config.configs[i].Id == selected_server.Id)
                            {
                                config.index = i;
                                match = true;
                                break;
                            }

                            if (config.configs[i].Group == selected_server.Group)
                            {
                                if (config.configs[i].IsMatchServer(selected_server))
                                {
                                    config.index = i;
                                    match = true;
                                    break;
                                }
                            }
                        }
                        if (!match)
                        {
                            config.index = config.configs.Count - 1;
                        }
                    }
                    else
                    {
                        config.index = config.configs.Count - 1;
                    }
                    if (count > 0)
                    {
                        foreach (var serverSubscribe in config.serverSubscribes.Where(serverSubscribe => serverSubscribe.URL == updateFreeNodeChecker.SubscribeTask.URL))
                        {
                            serverSubscribe.LastUpdateTime = (ulong)Math.Floor(DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);
                        }
                    }
                    controller.SaveServersConfig(config);
                }
            }

            if (count > 0)
            {
                if (updateFreeNodeChecker.Notify)
                {
                    _notifyIcon.ShowBalloonTip(I18N.GetString("Success"),
                    string.Format(I18N.GetString("Update subscribe {0} success"), lastGroup), BalloonIcon.Info);
                }
            }
            else
            {
                if (lastGroup == null)
                {
                    lastGroup = updateFreeNodeChecker.SubscribeTask.Group;
                }

                if (updateFreeNodeChecker.Notify)
                {
                    _notifyIcon.ShowBalloonTip(I18N.GetString("Error"),
                            string.Format(I18N.GetString("Update subscribe {0} failure"), lastGroup), BalloonIcon.Info);
                }
            }
            if (updateSubscribeManager.Next())
            {

            }
        }

        private void updateChecker_NewVersionFound(object sender, EventArgs e)
        {
            Application.Current.Dispatcher?.Invoke(() =>
            {
                if (updateChecker.Found)
                {
                    if (UpdateItem.Visibility != Visibility.Visible)
                    {
                        _notifyIcon.ShowBalloonTip(
                                string.Format(I18NUtil.GetAppStringValue(@"NewVersionFound"), UpdateChecker.Name,
                                        updateChecker.LatestVersionNumber),
                                I18NUtil.GetAppStringValue(@"ClickMenuToDownload"), BalloonIcon.Info);
                    }
                    UpdateItem.Visibility = Visibility.Visible;
                    UpdateItem.Header = string.Format(I18NUtil.GetAppStringValue(@"NewVersionAvailable"),
                            UpdateChecker.Name, updateChecker.LatestVersionNumber);
                }
            });
        }

        private void updateChecker_NewVersionNotFound(object sender, EventArgs e)
        {
            _notifyIcon.ShowBalloonTip($@"ShadowsocksR {UpdateChecker.FullVersion}", I18NUtil.GetAppStringValue(@"NewVersionNotFound"), BalloonIcon.Info);
        }

        private void UpdateChecker_NewVersionFoundFailed(object sender, EventArgs e)
        {
            _notifyIcon.ShowBalloonTip($@"ShadowsocksR {UpdateChecker.FullVersion}",
                    I18NUtil.GetAppStringValue(@"NewVersionFoundFailed"), BalloonIcon.Info);
        }

        private void UpdateItem_Clicked(object sender, RoutedEventArgs e)
        {
            Utils.OpenURL(updateChecker.LatestVersionUrl);
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
            noModifyItem.IsChecked = config.sysProxyMode == (int)ProxyMode.NoModify;
            enableItem.IsChecked = config.sysProxyMode == (int)ProxyMode.Direct;
            PACModeItem.IsChecked = config.sysProxyMode == (int)ProxyMode.Pac;
            globalModeItem.IsChecked = config.sysProxyMode == (int)ProxyMode.Global;
        }

        private void UpdateProxyRule(Configuration config)
        {
            ruleDisableBypass.IsChecked = config.proxyRuleMode == (int)ProxyRuleMode.Disable;
            ruleBypassLan.IsChecked = config.proxyRuleMode == (int)ProxyRuleMode.BypassLan;
            ruleBypassChina.IsChecked = config.proxyRuleMode == (int)ProxyRuleMode.BypassLanAndChina;
            ruleBypassNotChina.IsChecked = config.proxyRuleMode == (int)ProxyRuleMode.BypassLanAndNotChina;
            ruleUser.IsChecked = config.proxyRuleMode == (int)ProxyRuleMode.UserCustom;
        }

        private void LoadCurrentConfiguration()
        {
            var config = controller.GetCurrentConfiguration();
            UpdateServersMenu();
            UpdateSysProxyMode(config);

            UpdateProxyRule(config);

            SelectRandomItem.IsChecked = config.random;
            sameHostForSameTargetItem.IsChecked = config.sameHostForSameTarget;
            AutoCheckUpdateItem.IsChecked = config.AutoCheckUpdate;
            AllowPreReleaseItem.IsChecked = config.isPreRelease;
        }

        private void UpdateServersMenu()
        {
            var items = ServersItem.Items;
            while (!Equals(items[0], SeparatorItem))
            {
                items.RemoveAt(0);
            }

            var configuration = controller.GetCurrentConfiguration();
            var group = new SortedDictionary<string, MenuItem>();
            const string defGroup = @"!(no group)";
            var selectGroup = string.Empty;
            for (var i = 0; i < configuration.configs.Count; i++)
            {
                var server = configuration.configs[i];
                var groupName = string.IsNullOrEmpty(server.Group) ? defGroup : server.Group;

                var item = new MenuItem
                {
                    Header = server.FriendlyName,
                    Tag = i
                };
                item.Click += AServerItem_Click;
                if (configuration.index == i)
                {
                    item.IsChecked = true;
                    selectGroup = groupName;
                }

                if (group.ContainsKey(groupName))
                {
                    group[groupName].Items.Add(item);
                }
                else
                {
                    group[groupName] = CreateMenuGroup(groupName, new[] { item });
                }
            }
            var index = 0;
            foreach (var pair in group)
            {
                if (pair.Key == defGroup)
                {
                    pair.Value.Header = @"(empty group)";
                }

                if (pair.Key == selectGroup)
                {
                    pair.Value.Header = @"● " + pair.Value.Header;
                }
                else
                {
                    pair.Value.Header = @"　" + pair.Value.Header;
                }

                items.Insert(index, pair.Value);
                ++index;
            }
        }

        private void ShowConfigForm(bool addNode)
        {
            if (_configWindow != null)
            {
                _configWindow.Activate();
                _configWindow.UpdateLayout();
                if (_configWindow.WindowState == WindowState.Minimized)
                {
                    _configWindow.WindowState = WindowState.Normal;
                }
                if (addNode)
                {
                    var cfg = controller.GetCurrentConfiguration();
                    _configWindow.SetServerListSelectedIndex(cfg.index + 1);
                }
            }
            else
            {
                configFrom_open = true;
                _configWindow = new ConfigWindow(controller, addNode ? -1 : -2);
                _configWindow.Show();
                _configWindow.Activate();
                _configWindow.BringToFront();
                _configWindow.Closed += ConfigWindow_Closed;
            }
        }

        private void ConfigWindow_Closed(object sender, EventArgs e)
        {
            _configWindow = null;
            configFrom_open = false;
            Utils.ReleaseMemory();
            if (eventList.Count > 0)
            {
                foreach (var p in eventList)
                {
                    updateFreeNodeChecker_NewFreeNodeFound(p.sender, p.e);
                }

                eventList.Clear();
            }
        }

        private void ShowConfigForm(int index)
        {
            if (_configWindow != null)
            {
                _configWindow.Activate();
                _configWindow.UpdateLayout();
                if (_configWindow.WindowState == WindowState.Minimized)
                {
                    _configWindow.WindowState = WindowState.Normal;
                }
                _configWindow.SetServerListSelectedIndex(index);
            }
            else
            {
                configFrom_open = true;
                _configWindow = new ConfigWindow(controller, index);
                _configWindow.Show();
                _configWindow.Activate();
                _configWindow.BringToFront();
                _configWindow.Closed += ConfigWindow_Closed;
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
                _settingsWindow.Closed += (o, args) =>
                {
                    _settingsWindow = null;
                    Utils.ReleaseMemory();
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
                    Utils.ReleaseMemory();
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
                    Utils.ReleaseMemory();
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
                    Utils.ReleaseMemory();
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
                    Utils.ReleaseMemory();
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
                    var cfg = Configuration.LoadFile(name);
                    if (cfg.configs.Count == 1 && cfg.configs[0].server == Configuration.GetDefaultServer().server)
                    {
                        MessageBox.Show(@"Load config file failed", UpdateChecker.Name);
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

        public void Quit_Click(object sender, EventArgs e)
        {
            controller.Stop();
            if (_configWindow != null)
            {
                _configWindow.Close();
                _configWindow = null;
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

        private void OpenWiki_Click(object sender, RoutedEventArgs e)
        {
            Utils.OpenURL(@"https://github.com/HMBSbige/ShadowsocksR-Windows/wiki");
        }

        private void FeedbackItem_Click(object sender, RoutedEventArgs e)
        {
            Utils.OpenURL(@"https://github.com/HMBSbige/ShadowsocksR-Windows/issues/new/choose");
        }

        private void ResetPasswordItem_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ResetPassword();
            dlg.Show();
            dlg.Activate();
        }

        private void AboutItem_Click(object sender, RoutedEventArgs e)
        {
            Utils.OpenURL(@"https://github.com/HMBSbige/ShadowsocksR-Windows");
        }

        private void DonateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Utils.OpenURL(@"https://github.com/HMBSbige/ShadowsocksR-Windows/blob/master/pic/wechat.jpg");
        }

        private void notifyIcon_TrayLeftMouseUp(object sender, RoutedEventArgs e)
        {
            var key = Keyboard.IsKeyDown(Key.LeftShift) ? 1 : 0;
            key |= Keyboard.IsKeyDown(Key.LeftCtrl) ? 2 : 0;
            key |= Keyboard.IsKeyDown(Key.LeftAlt) ? 4 : 0;
            if (key == 2)
            {
                ShowServerLogForm();
            }
            else if (key == 1)
            {
                ShowSettingForm();
            }
            else if (key == 4)
            {
                ShowPortMapForm();
            }
            else
            {
                ShowConfigForm(false);
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
            Task.Run(() => { controller.ToggleRuleMode((int)ProxyRuleMode.BypassLan); });
        }

        private void RuleBypassChinaItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { controller.ToggleRuleMode((int)ProxyRuleMode.BypassLanAndChina); });
        }

        private void RuleBypassNotChinaItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { controller.ToggleRuleMode((int)ProxyRuleMode.BypassLanAndNotChina); });
        }

        private void RuleUserItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { controller.ToggleRuleMode((int)ProxyRuleMode.UserCustom); });
        }

        private void RuleBypassDisableItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { controller.ToggleRuleMode((int)ProxyRuleMode.Disable); });
        }

        private void SelectRandomItem_Click(object sender, RoutedEventArgs e)
        {
            SelectRandomItem.IsChecked = !SelectRandomItem.IsChecked;
            controller.ToggleSelectRandom(SelectRandomItem.IsChecked);
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
            controller.ToggleSameHostForSameTargetRandom(sameHostForSameTargetItem.IsChecked);
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
            controller.UpdatePACFromChnDomainsAndIP(ChnDomainsAndIPUpdater.Templates.ss_white);
        }

        private void UpdatePACFromCNOnlyListItem_Click(object sender, RoutedEventArgs e)
        {
            controller.UpdatePACFromChnDomainsAndIP(ChnDomainsAndIPUpdater.Templates.ss_white_r);
        }

        private void UpdatePACFromCNIPListItem_Click(object sender, RoutedEventArgs e)
        {
            controller.UpdatePACFromChnDomainsAndIP(ChnDomainsAndIPUpdater.Templates.ss_cnip);
        }

        private void EditUserRuleFileForGFWListItem_Click(object sender, RoutedEventArgs e)
        {
            controller.TouchUserRuleFile();
        }

        private void AServerItem_Click(object sender, EventArgs e)
        {
            var config = controller.GetCurrentConfiguration();
            Console.WriteLine(@"config.checkSwitchAutoCloseAll:" + config.checkSwitchAutoCloseAll);
            if (config.checkSwitchAutoCloseAll)
            {
                controller.DisconnectAllConnections();
            }
            var item = (MenuItem)sender;
            controller.SelectServerIndex((int)item.Tag);
        }

        private void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            updateChecker.Check(controller.GetCurrentConfiguration(), true);
        }

        private void CheckNodeUpdate_Click(object sender, RoutedEventArgs e)
        {
            updateSubscribeManager.CreateTask(controller.GetCurrentConfiguration(), updateFreeNodeChecker, -1, true, true);
        }

        private void CheckNodeUpdateBypassProxy_Click(object sender, RoutedEventArgs e)
        {
            updateSubscribeManager.CreateTask(controller.GetCurrentConfiguration(), updateFreeNodeChecker, -1, false, true);
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
            controller.DisconnectAllConnections();
        }

        public void ImportAddress(string text)
        {
            var urls = new List<string>();
            Utils.URL_Split(text, ref urls);
            var count = urls.Count(url => controller.AddServerBySSURL(url));
            if (count > 0)
            {
                ShowConfigForm(true);
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

        [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
        private void ScanQRCodeItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                var w = (int)SystemParameters.VirtualScreenWidth;
                var h = (int)SystemParameters.VirtualScreenHeight;
                var x = (int)SystemParameters.VirtualScreenLeft;
                var y = (int)SystemParameters.VirtualScreenTop;
                using var fullImage = new Bitmap(w, h);
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
                        var success = controller.AddServerBySSURL(result.Text);
                        Application.Current.Dispatcher?.Invoke(() =>
                        {
                            var splash = new QRCodeSplashWindow();
                            if (success)
                            {
                                splash.Closed += Splash_Closed;
                            }
                            else
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
                            splash.Show();
                        });
                        return;
                    }
                }

                MessageBox.Show(I18N.GetString(@"No QRCode found. Try to zoom in or move it to the center of the screen."));
            });
        }

        private void Splash_Closed(object sender, EventArgs e)
        {
            ShowConfigForm(true);
        }

        private void showURLFromQRCode()
        {
            var dlg = new ShowTextWindow(_urlToOpen);
            dlg.Show();
            dlg.Activate();
            dlg.BringToFront();
        }

        private void Splash_Closed2(object sender, EventArgs e)
        {
            showURLFromQRCode();
        }

        private void showURLFromQRCode(object sender, RoutedEventArgs e)
        {
            showURLFromQRCode();
        }

        #endregion
    }
}
