using Shadowsocks.Controller;
using Shadowsocks.Controller.Service;
using Shadowsocks.Model;
using Shadowsocks.ViewModel;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Shadowsocks.View
{
    public partial class ServerLogWindow
    {
        public ServerLogWindow(ShadowsocksController controller, WindowStatus status)
        {
            InitializeComponent();
            _controller = controller;
            Closed += (o, e) => { _controller.ConfigChanged -= controller_ConfigChanged; };
            _controller.ConfigChanged += controller_ConfigChanged;
            LoadLanguage();
            LoadConfig();
            if (status == null)
            {
                SizeToContent = SizeToContent.Width;
                Height = 600;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            else
            {
                SizeToContent = SizeToContent.Manual;
                WindowStartupLocation = WindowStartupLocation.Manual;
                status.SetStatus(this);
            }
        }

        private void LoadConfig()
        {
            UpdateTitle();
            ServerLogViewModel.ReadConfig(_controller);
            if (ServerLogViewModel.SelectedServer != null)
            {
                ServerDataGrid.ScrollIntoView(ServerLogViewModel.SelectedServer, ServerColumn);
            }
        }

        private void controller_ConfigChanged(object sender, EventArgs e)
        {
            LoadConfig();
        }

        private readonly ShadowsocksController _controller;
        public ServerLogViewModel ServerLogViewModel { get; set; } = new ServerLogViewModel();

        private void ServerDataGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ServerDataGrid.SelectedCells.Count > 0 && ServerDataGrid.SelectedCells[0].Column != null)
            {
                if (ServerDataGrid.SelectedCells[0].Column.Header is string header && ServerDataGrid.SelectedCells[0].Item is Server serverObject)
                {
                    var id = serverObject.Index - 1;
                    if (header == I18N.GetString(@"Server"))
                    {
                        var config = _controller.GetCurrentConfiguration();
                        Console.WriteLine($@"config.checkSwitchAutoCloseAll:{config.checkSwitchAutoCloseAll}");
                        if (config.checkSwitchAutoCloseAll)
                        {
                            _controller.DisconnectAllConnections();
                        }
                        _controller.SelectServerIndex(id);
                    }
                    else if (header == I18N.GetString(@"Enable"))
                    {
                        var server = ServerLogViewModel.ServersCollection[id];
                        server.Enable = !server.Enable;
                        _controller.Save();
                    }
                    else if (header == I18N.GetString(@"Group"))
                    {
                        var currentServer = ServerLogViewModel.ServersCollection[id];
                        var group = currentServer.Group;
                        if (!string.IsNullOrEmpty(group))
                        {
                            var enable = !currentServer.Enable;
                            foreach (var server in ServerLogViewModel.ServersCollection)
                            {
                                if (server.Group == group)
                                {
                                    if (server.Enable != enable)
                                    {
                                        server.Enable = enable;
                                    }
                                }
                            }
                            _controller.Save();
                        }
                    }
                    else
                    {
                        ServerDataGrid.SelectedCells.Clear();
                        ServerDataGrid.CurrentCell = new DataGridCellInfo(serverObject, ServerDataGrid.Columns[0]);
                        ServerDataGrid.SelectedCells.Add(ServerDataGrid.CurrentCell);
                    }
                }
            }
        }

        private void ServerDataGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }
            if (ServerDataGrid.SelectedCells.Count > 0 && ServerDataGrid.SelectedCells[0].Column != null)
            {
                if (ServerDataGrid.SelectedCells[0].Column.Header is string header && ServerDataGrid.SelectedCells[0].Item is Server serverObject)
                {
                    var id = serverObject.Index - 1;
                    if (header == I18N.GetString(@"ID"))
                    {
                        _controller.ShowConfigForm(id);
                    }
                    else if (header == I18N.GetString(@"Connecting"))
                    {
                        var server = ServerLogViewModel.ServersCollection[id];
                        server.GetConnections().CloseAll();
                    }
                    else if (header == I18N.GetString(@"Max UpSpeed") || header == I18N.GetString(@"Max DSpeed"))
                    {
                        ServerLogViewModel.ServersCollection[id].SpeedLog.ClearMaxSpeed();
                    }
                    else if (header == I18N.GetString(@"Dload") || header == I18N.GetString(@"Upload"))
                    {
                        ServerLogViewModel.ServersCollection[id].SpeedLog.ClearTrans();
                    }
                    else if (header == I18N.GetString(@"DloadRaw"))
                    {
                        ServerLogViewModel.ServersCollection[id].SpeedLog.Clear();
                        ServerLogViewModel.ServersCollection[id].Enable = true;
                    }
                    else if (header == I18N.GetString(@"Error")
                    || header == I18N.GetString(@"Timeout")
                    || header == I18N.GetString(@"Empty Response")
                    || header == I18N.GetString(@"Continuous")
                    || header == I18N.GetString(@"Error Percent")
                    )
                    {
                        ServerLogViewModel.ServersCollection[id].SpeedLog.ClearError();
                        ServerLogViewModel.ServersCollection[id].Enable = true;
                    }
                    else
                    {
                        ServerDataGrid.SelectedCells.Clear();
                        ServerDataGrid.CurrentCell = new DataGridCellInfo(serverObject, ServerDataGrid.Columns[0]);
                        ServerDataGrid.SelectedCells.Add(ServerDataGrid.CurrentCell);
                    }
                }
            }
        }

        private void UpdateTitle()
        {
            Title = $@"{I18N.GetString(@"ServerLog")}({(_controller.GetCurrentConfiguration().shareOverLan ? I18N.GetString(@"Any") : I18N.GetString(@"Local"))}:{_controller.GetCurrentConfiguration().localPort} {I18N.GetString(@"Version")}{UpdateChecker.FullVersion})";
        }

        private void LoadLanguage()
        {
            ControlMenuItem.Header = I18N.GetString(@"_Control");
            DisconnectDirectMenuItem.Header = I18N.GetString(@"_Disconnect direct connections");
            DisconnectAllMenuItem.Header = I18N.GetString(@"Disconnect _All");
            ClearMaxMenuItem.Header = I18N.GetString(@"Clear _MaxSpeed");
            ClearAllMenuItem.Header = I18N.GetString(@"_Clear");
            ClearSelectedTotalMenuItem.Header = I18N.GetString(@"Clear _Selected Total");
            ClearTotalMenuItem.Header = I18N.GetString(@"Clear _Total");

            OutMenuItem.Header = I18N.GetString(@"Port _out");
            CopyCurrentLinkMenuItem.Header = I18N.GetString(@"Copy current link");
            CopyCurrentGroupLinksMenuItem.Header = I18N.GetString(@"Copy current group links");
            CopyAllEnableLinksMenuItem.Header = I18N.GetString(@"Copy all enable links");
            CopyAllLinksMenuItem.Header = I18N.GetString(@"Copy all links");

            WindowMenuItem.Header = I18N.GetString(@"_Window");
            AutoSizeMenuItem.Header = I18N.GetString(@"Auto _size");
            AlwaysTopMenuItem.Header = I18N.GetString(@"Always On _Top");

            foreach (var column in ServerDataGrid.Columns)
            {
                column.Header = I18N.GetString(column.Header as string);
            }
        }

        private void AlwaysTopMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
        }

        private void AutoSizeMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            foreach (var column in ServerDataGrid.Columns)
            {
                column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
            SizeToContent = SizeToContent.Width;
        }

        private void DisconnectDirectMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            Server.GetForwardServerRef().GetConnections().CloseAll();
        }

        private void DisconnectAllMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            _controller.DisconnectAllConnections();
            Server.GetForwardServerRef().GetConnections().CloseAll();
        }

        private void ClearMaxMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var config = _controller.GetCurrentConfiguration();
            foreach (var server in config.configs)
            {
                server.SpeedLog.ClearMaxSpeed();
            }
        }

        private void ClearAllMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var config = _controller.GetCurrentConfiguration();
            foreach (var server in config.configs)
            {
                server.SpeedLog.Clear();
            }
        }

        private void ClearSelectedTotalMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var config = _controller.GetCurrentConfiguration();
            if (config.index >= 0 && config.index < config.configs.Count)
            {
                try
                {
                    _controller.ClearTransferTotal(config.configs[config.index].server);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void ClearTotalMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var config = _controller.GetCurrentConfiguration();
            foreach (var server in config.configs)
            {
                _controller.ClearTransferTotal(server.server);
            }
        }

        private void CopyCurrentLinkMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var config = _controller.GetCurrentConfiguration();
            if (config.index >= 0 && config.index < config.configs.Count)
            {
                var link = config.configs[config.index].SsrLink;
                Clipboard.SetDataObject(link);
            }
        }

        private void CopyCurrentGroupLinksMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var config = _controller.GetCurrentConfiguration();
            if (config.index >= 0 && config.index < config.configs.Count)
            {
                var group = config.configs[config.index].Group;
                var link = config.configs.Where(t => t.Group == group).Aggregate(string.Empty, (current, t) => current + $@"{t.SsrLink}{Environment.NewLine}");
                Clipboard.SetDataObject(link);
            }
        }

        private void CopyAllEnableLinksMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var config = _controller.GetCurrentConfiguration();
            var link = config.configs.Where(t => t.Enable).Aggregate(string.Empty, (current, t) => current + $@"{t.SsrLink}{Environment.NewLine}");
            Clipboard.SetDataObject(link);
        }

        private void CopyAllLinksMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var config = _controller.GetCurrentConfiguration();
            var link = config.configs.Aggregate(string.Empty, (current, t) => current + $@"{t.SsrLink}{Environment.NewLine}");
            Clipboard.SetDataObject(link);
        }

        private void ServerLogWindow_OnStateChanged(object sender, EventArgs e)
        {
            if (ServerDataGrid.SelectedCells.Count > 0 && ServerDataGrid.SelectedCells[0].Column != null)
            {
                if (ServerDataGrid.SelectedCells[0].Item is Server serverObject)
                {
                    ServerDataGrid.ScrollIntoView(serverObject, ServerColumn);
                    return;
                }
            }
            if (ServerLogViewModel.SelectedServer != null)
            {
                ServerDataGrid.ScrollIntoView(ServerLogViewModel.SelectedServer, ServerColumn);
            }
        }
    }
}
