using Shadowsocks.Controller;
using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Model;
using Shadowsocks.Util;
using Shadowsocks.ViewModel;
using Syncfusion.Data;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.UI.Xaml.Grid.Helpers;
using Syncfusion.UI.Xaml.ScrollAxis;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Shadowsocks.View
{
    public partial class ServerLogWindow
    {
        public ServerLogWindow(ShadowsocksController controller, WindowStatus status)
        {
            InitializeComponent();
            I18NUtil.SetLanguage(Resources, @"ServerLogWindow");
            LoadLanguage();

            _controller = controller;
            Closed += (o, e) => { _controller.ConfigChanged -= controller_ConfigChanged; };
            _controller.ConfigChanged += controller_ConfigChanged;
            LoadConfig(true);

            ServerDataGrid.GridColumnSizer.SortIconWidth = 0;
            if (status == null)
            {
                SizeToContent = SizeToContent.Width;
                Height = 600;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                ServerDataGrid.ShowBusyIndicator = false;
            }
            else
            {
                ServerDataGrid.ShowBusyIndicator = true;
                SizeToContent = SizeToContent.Manual;
                status.SetStatus(this);
            }
        }

        private void LoadLanguage()
        {
            ServerDataGrid.Columns[Resources[@"IndexMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"Index");
            ServerDataGrid.Columns[Resources[@"GroupMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"Group");
            ServerDataGrid.Columns[Resources[@"ServerMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"Server");
            ServerDataGrid.Columns[Resources[@"ConnectingMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"Connecting");
            ServerDataGrid.Columns[Resources[@"AvgConnectTimeMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"Latency");
            ServerDataGrid.Columns[Resources[@"AvgDownloadBytesMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"AvgDSpeed");
            ServerDataGrid.Columns[Resources[@"MaxDownSpeedMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"MaxDSpeed");
            ServerDataGrid.Columns[Resources[@"AvgUploadBytesMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"AvgUpSpeed");
            ServerDataGrid.Columns[Resources[@"MaxUpSpeedMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"MaxUpSpeed");
            ServerDataGrid.Columns[Resources[@"TotalDownloadBytesMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"Dload");
            ServerDataGrid.Columns[Resources[@"TotalUploadBytesMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"Upload");
            ServerDataGrid.Columns[Resources[@"TotalDownloadRawBytesMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"DloadRaw");
            ServerDataGrid.Columns[Resources[@"ConnectErrorMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"Error");
            ServerDataGrid.Columns[Resources[@"ErrorTimeoutTimesMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"Timeout");
            ServerDataGrid.Columns[Resources[@"ErrorEmptyTimesMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"EmptyResponse");
            ServerDataGrid.Columns[Resources[@"ErrorContinuousTimesMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"Continuous");
            ServerDataGrid.Columns[Resources[@"ErrorPercentMappingName"].ToString()].HeaderText = this.GetWindowStringValue(@"ErrorPercent");
        }

        private void LoadConfig(bool isFirstLoad)
        {
            UpdateTitle();
            ServerDataGrid.View?.BeginInit();
            ServerLogViewModel.ReadConfig(_controller);
            ServerDataGrid.View?.EndInit();

            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (isFirstLoad && ServerLogViewModel.SelectedServer != null)
                {
                    ServerDataGrid.ScrollInView(new RowColumnIndex(ServerLogViewModel.SelectedServer.Index, 2));
                }
            }));
        }

        private void controller_ConfigChanged(object sender, EventArgs e)
        {
            LoadConfig(false);
        }

        private readonly ShadowsocksController _controller;
        public ServerLogViewModel ServerLogViewModel { get; set; } = new ServerLogViewModel();

        private void UpdateTitle()
        {
            Title = $@"{this.GetWindowStringValue(@"Title")}({(_controller.GetCurrentConfiguration().shareOverLan ? this.GetWindowStringValue(@"Any") : this.GetWindowStringValue(@"Local"))}:{_controller.GetCurrentConfiguration().localPort} {this.GetWindowStringValue(@"Version")}{UpdateChecker.FullVersion})";
        }

        private void AlwaysTopMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
        }

        private void AutoSizeMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            //Refreshing auto size calculation
            ServerDataGrid.GridColumnSizer.ResetAutoCalculationforAllColumns();
            ServerDataGrid.GridColumnSizer.Refresh();
            foreach (var column in ServerDataGrid.Columns.Where(column => !double.IsNaN(column.Width)))
            {
                column.Width = double.NaN;
            }
            ServerDataGrid.GridColumnSizer.Refresh();
            SizeToContent = SizeToContent.Width;
        }

        private void DisconnectDirectMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            Server.GetForwardServerRef().Connections.CloseAll();
        }

        private void DisconnectAllMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            _controller.DisconnectAllConnections();
            Server.GetForwardServerRef().Connections.CloseAll();
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
                    _controller.ClearTransferTotal(config.configs[config.index].Id);
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
                _controller.ClearTransferTotal(server.Id);
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

        private void ServerDataGrid_OnCellTapped(object sender, GridCellTappedEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (ServerDataGrid.CurrentColumn != null && ServerDataGrid.SelectedItem is Server server)
            {
                var index = server.Index - 1;
                var mappingName = ServerDataGrid.CurrentColumn.MappingName;
                if (mappingName == Resources[@"ServerMappingName"].ToString())
                {
                    _controller.DisconnectAllConnections(true);
                    _controller.SelectServerIndex(index);
                }
                else if (mappingName == Resources[@"GroupMappingName"].ToString())
                {
                    var group = server.Group;
                    if (!string.IsNullOrEmpty(group))
                    {
                        var enable = !server.Enable;
                        foreach (var sameGroupServer in ServerLogViewModel.ServersCollection)
                        {
                            if (sameGroupServer.Group == group)
                            {
                                sameGroupServer.Enable = enable;
                            }
                        }
                        _controller.Save();
                    }
                }
            }
        }

        private void ServerDataGrid_OnCellDoubleTapped(object sender, GridCellDoubleTappedEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (ServerDataGrid.CurrentColumn != null && ServerDataGrid.SelectedItem is Server server)
            {
                var index = server.Index - 1;
                var mappingName = ServerDataGrid.CurrentColumn.MappingName;
                if (mappingName == Resources[@"IndexMappingName"].ToString())
                {
                    _controller.ShowConfigForm(index);
                }
                else if (mappingName == Resources[@"ConnectingMappingName"].ToString())
                {
                    server.Connections.CloseAll();
                }
                else if (mappingName == Resources[@"MaxDownSpeedMappingName"].ToString()
                        || mappingName == Resources[@"MaxUpSpeedMappingName"].ToString())
                {
                    server.SpeedLog.ClearMaxSpeed();
                }
                else if (mappingName == Resources[@"TotalDownloadBytesMappingName"].ToString()
                         || mappingName == Resources[@"TotalUploadBytesMappingName"].ToString())
                {
                    server.SpeedLog.ClearTrans();
                }
                else if (mappingName == Resources[@"TotalDownloadRawBytesMappingName"].ToString())
                {
                    server.SpeedLog.Clear();
                    server.Enable = true;
                }
                else if (mappingName == Resources[@"ConnectErrorMappingName"].ToString()
                         || mappingName == Resources[@"ErrorTimeoutTimesMappingName"].ToString()
                         || mappingName == Resources[@"ErrorEmptyTimesMappingName"].ToString()
                         || mappingName == Resources[@"ErrorContinuousTimesMappingName"].ToString()
                         || mappingName == Resources[@"ErrorPercentMappingName"].ToString())
                {
                    server.SpeedLog.ClearError();
                    server.Enable = true;
                }
                else
                {
                    ServerDataGrid.ClearSelections(false);
                    ServerDataGrid.SelectCell(server, ServerDataGrid.Columns[0]);
                }
            }
        }

        private void ServerDataGrid_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var visualContainer = ServerDataGrid.GetVisualContainer();
            var rowColumnIndex = visualContainer.PointToCellRowColumnIndex(e.GetPosition(visualContainer));
            if (rowColumnIndex.IsEmpty)
                return;

            var columnIndex = ServerDataGrid.ResolveToGridVisibleColumnIndex(rowColumnIndex.ColumnIndex);
            if (columnIndex != -1)
                return;

            var recordIndex = ServerDataGrid.ResolveToRecordIndex(rowColumnIndex.RowIndex);
            if (recordIndex == -1)
            {
                const string columnName = @"Enable";
                var sortColumnDescription = ServerDataGrid.SortColumnDescriptions.FirstOrDefault(col => col.ColumnName == columnName);
                if (sortColumnDescription != null)
                {
                    sortColumnDescription.SortDirection = sortColumnDescription.SortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
                    ServerDataGrid.SortColumnDescriptions.Remove(sortColumnDescription);
                }
                else
                {
                    sortColumnDescription = new SortColumnDescription
                    {
                        ColumnName = columnName,
                        SortDirection = ListSortDirection.Ascending
                    };
                }
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                }
                else
                {
                    ServerDataGrid.SortColumnDescriptions.Clear();
                }
                ServerDataGrid.SortColumnDescriptions.Add(sortColumnDescription);
            }
            else
            {
                var entry = ServerDataGrid.View.GroupDescriptions.Count == 0
                        ? ServerDataGrid.View.Records[recordIndex]
                        : ServerDataGrid.View.TopLevelGroup.DisplayElements[recordIndex];
                if (entry.IsRecords && entry is RecordEntry recordEntry && recordEntry.Data is Server server)
                {
                    server.Enable = !server.Enable;
                    _controller.Save();
                }
            }
        }
    }
}
