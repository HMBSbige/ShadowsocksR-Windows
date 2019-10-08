using Shadowsocks.Controller;
using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Encryption;
using Shadowsocks.Model;
using Shadowsocks.Util;
using Shadowsocks.ViewModel;
using Syncfusion.Windows.Tools.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Shadowsocks.View
{
    public partial class ConfigWindow
    {
        public ConfigWindow(ShadowsocksController controller, int focusIndex)
        {
            InitializeComponent();
            I18NUtil.SetLanguage(Resources, @"ConfigWindow");
            SizeChanged += (o, args) => { GenQr(LinkTextBox.Text); };
            Splitter2.DragDelta += (o, args) => { GenQr(LinkTextBox.Text); };
            Closed += (o, e) =>
            {
                _controller.ConfigChanged -= controller_ConfigChanged;
                ServerViewModel.ServersChanged -= ServerViewModel_ServersChanged;
            };

            _controller = controller;
            foreach (var name in from name in EncryptorFactory.GetEncryptor().Keys let info = EncryptorFactory.GetEncryptorInfo(name) where info.display select name)
            {
                EncryptionComboBox.Items.Add(name);
            }
            foreach (var protocol in Protocols)
            {
                ProtocolComboBox.Items.Add(protocol);
            }
            foreach (var obfs in ObfsStrings)
            {
                ObfsComboBox.Items.Add(obfs);
            }

            _controller.ConfigChanged += controller_ConfigChanged;
            ServerViewModel.ServersChanged += ServerViewModel_ServersChanged;
            _focusIndex = focusIndex;
            ServerGroupBox.Visibility = ServersTreeView.SelectedValue == null ? Visibility.Hidden : Visibility.Visible;
        }

        private void ServerViewModel_ServersChanged(object sender, EventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        private static readonly string[] Protocols = {
                "origin",
                "verify_deflate",
                "auth_sha1_v4",
                "auth_aes128_md5",
                "auth_aes128_sha1",
                "auth_chain_a",
                "auth_chain_b",
                "auth_chain_c",
                "auth_chain_d",
                "auth_chain_e",
                "auth_chain_f",
                "auth_akarin_rand",
                "auth_akarin_spec_a"
        };

        private static readonly string[] ObfsStrings = {
                "plain",
                "http_simple",
                "http_post",
                "random_head",
                "tls1.2_ticket_auth",
                "tls1.2_ticket_fastauth"
        };

        private readonly ShadowsocksController _controller;

        private Configuration _modifiedConfiguration;
        private int _focusIndex;

        public ServerViewModel ServerViewModel { get; set; } = new ServerViewModel();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTitle();
            LoadCurrentConfiguration(false);
            switch (_focusIndex)
            {
                case -1:
                {
                    var index = _modifiedConfiguration.index + 1;
                    if (index < 0 || index > _modifiedConfiguration.configs.Count)
                        index = _modifiedConfiguration.configs.Count;
                    _focusIndex = index;
                    break;
                }
                case -2:
                {
                    var index = _modifiedConfiguration.index;
                    if (index < 0 || index > _modifiedConfiguration.configs.Count)
                        index = _modifiedConfiguration.configs.Count;
                    _focusIndex = index;
                    break;
                }
            }

            if (_focusIndex >= 0 && _focusIndex < _modifiedConfiguration.configs.Count)
            {
                MoveToSelectedItem(_focusIndex);
            }
        }

        private void UpdateTitle()
        {
            Title = $@"{this.GetWindowStringValue(@"Title")}({(_controller.GetCurrentConfiguration().shareOverLan ? this.GetWindowStringValue(@"Any") : this.GetWindowStringValue(@"Local"))}:{_controller.GetCurrentConfiguration().localPort} {this.GetWindowStringValue(@"Version")}:{UpdateChecker.FullVersion})";
        }

        private void controller_ConfigChanged(object sender, EventArgs e)
        {
            LoadCurrentConfiguration(true);
            UpdateTitle();
        }

        private void LoadCurrentConfiguration(bool scrollToSelectedItem)
        {
            _modifiedConfiguration = _controller.GetConfiguration();
            ServerViewModel.ReadServers(_modifiedConfiguration.configs);

            if (scrollToSelectedItem)
            {
                MoveToSelectedItem(_modifiedConfiguration.index);
            }

            ApplyButton.IsEnabled = false;
        }

        #region TreeView

        private void ExpandTree(bool expand)
        {
            foreach (var node in ServerTreeViewModel.GetNodes(ServerViewModel.ServersTreeViewCollection))
            {
                if (node.Type != ServerTreeViewType.Server)
                {
                    node.IsExpanded = expand;
                }
            }
        }

        public void MoveToSelectedItem(int index)
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (index >= 0 && index < _modifiedConfiguration.configs.Count)
                {
                    var server = _modifiedConfiguration.configs[index];
                    MoveToSelectedItem(server.Id);
                }
            }));
        }

        private void MoveToSelectedItem(string id)
        {
            var serverTreeViewModel = ServerTreeViewModel.FindNode(ServerViewModel.ServersTreeViewCollection, id);
            if (serverTreeViewModel != null)
            {
                MoveToSelectedItem(serverTreeViewModel);
            }
        }

        private void MoveToSelectedItem(IVirtualTree serverTreeViewModel)
        {
            serverTreeViewModel.IsExpanded = true;
            var parent = serverTreeViewModel.Parent;
            while (parent != null)
            {
                parent.IsExpanded = true;
                parent = parent.Parent;
            }

            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                var treeViewItem = ServersTreeView.GetContainerFromItem(serverTreeViewModel);
                if (treeViewItem != null)
                {
                    ServersTreeView.BringIntoView(treeViewItem);
                    if (ServersTreeView.SelectedItems.Count == 1 && ReferenceEquals(ServersTreeView.SelectedItem, treeViewItem.Header))
                    {
                        //Fix a weird selection action
                    }
                    else
                    {
                        ServersTreeView.ClearSelection();
                        serverTreeViewModel.IsSelected = true;
                    }
                }
            }));
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServersTreeView.SelectedItem is ServerTreeViewModel st)
            {
                switch (st.Type)
                {
                    case ServerTreeViewType.Subtag:
                        MessageBox.Show(this.GetWindowStringValue(@"AddServerError"), UpdateChecker.Name, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    case ServerTreeViewType.Group:
                    {
                        var item = new ServerTreeViewModel
                        {
                            Type = ServerTreeViewType.Server,
                            Server = new Server { Group = st.Name },
                            Parent = st
                        };
                        st.Nodes.Add(item);
                        MoveToSelectedItem(item);
                        return;
                    }
                    case ServerTreeViewType.Server:
                    {
                        if (st.Parent is ServerTreeViewModel parent)
                        {
                            var item = new ServerTreeViewModel
                            {
                                Type = ServerTreeViewType.Server,
                                Server = Server.Clone(st.Server),
                                Parent = parent
                            };
                            var index = parent.Nodes.IndexOf(st) + 1;
                            parent.Nodes.Insert(index, item);
                            MoveToSelectedItem(item);
                        }
                        return;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            var newServer = new Server();
            ServerViewModel.ReadServers(new List<Server> { newServer });
            MoveToSelectedItem(newServer.Id);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var deleteItems = ServersTreeView.SelectedItems.ToArray();
            foreach (var selectedItem in deleteItems)
            {
                if (selectedItem is ServerTreeViewModel st)
                {
                    ServerTreeViewModel.Remove(ServerViewModel.ServersTreeViewCollection, st);
                }
            }
            //Fix weird selections
            ServersTreeView.ClearSelection();
        }

        private void Expand_OnClick(object sender, RoutedEventArgs e)
        {
            ExpandTree(true);
        }

        private void Collapse_Click(object sender, RoutedEventArgs e)
        {
            ExpandTree(false);
        }

        private void ServersTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            ServerGroupBox.Visibility = ServersTreeView.SelectedValue == null ? Visibility.Hidden : Visibility.Visible;
        }

        private void ServersTreeView_OnDragEnd(object sender, DragTreeViewItemAdvEventArgs e)
        {
            //Fix weird selections
            ServersTreeView.ClearSelection();

            var draggingItems = new List<ServerTreeViewModel>();
            foreach (var draggingItem in e.DraggingItems)
            {
                if (draggingItem.Header is ServerTreeViewModel st)
                {
                    draggingItems.Add(st);
                }
                else
                {
                    goto Skip;
                }
            }
            var types = new HashSet<ServerTreeViewType>(draggingItems.Select(items => items.Type));
            if (types.Count == 1)
            {
                var type = types.First();
                switch (type)
                {
                    case ServerTreeViewType.Subtag:
                        e.Cancel = !(e.TargetDropItem is TreeViewAdv);
                        return;
                    case ServerTreeViewType.Group:
                        //移到相同订阅组上会被吃掉
                        if (e.DropIndex < 0)
                        {
                            goto Skip;
                        }
                        //属于同样的订阅
                        if (e.TargetDropItem is TreeViewItemAdv treeViewItem)
                        {
                            if (treeViewItem.Header is ServerTreeViewModel st && st.Type == ServerTreeViewType.Subtag)
                            {
                                if (draggingItems.All(draggingItem => draggingItem.Parent == st))
                                {
                                    e.Cancel = false;
                                    return;
                                }
                            }
                        }
                        break;
                    case ServerTreeViewType.Server:
                        //属于同一个订阅
                        var subs = new HashSet<string>();
                        foreach (var draggingItem in draggingItems)
                        {
                            var parent = draggingItem.Parent;
                            while (parent.Parent != null)
                            {
                                parent = parent.Parent;
                            }
                            if (parent is ServerTreeViewModel parentModel)
                            {
                                subs.Add(parentModel.Name);
                                continue;
                            }
                            goto Skip;
                        }
                        if (subs.Count != 1)
                        {
                            goto Skip;
                        }
                        var sub = subs.First();
                        //且 目标为群组或原版的订阅连接
                        if (e.TargetDropItem is TreeViewItemAdv treeViewItemAdv)
                        {
                            if (treeViewItemAdv.Header is ServerTreeViewModel st)
                            {
                                switch (st.Type)
                                {
                                    case ServerTreeViewType.Subtag:
                                    {
                                        //移到相同订阅组上会被吃掉
                                        if (e.DropIndex < 0)
                                        {
                                            goto Skip;
                                        }
                                        //原版的订阅连接
                                        if (draggingItems.All(draggingItem => draggingItem.Parent == st))
                                        {
                                            e.Cancel = false;
                                            return;
                                        }
                                        break;
                                    }
                                    case ServerTreeViewType.Group:
                                    {
                                        //相同订阅组
                                        if (st.Parent is ServerTreeViewModel targetParent && sub == targetParent.Name)
                                        {
                                            //同一个组
                                            var parents = new HashSet<IVirtualTree>(draggingItems.Select(items => items.Parent));
                                            if (parents.Count != 1)
                                            {
                                                goto Skip;
                                            }
                                            var parent = parents.First();
                                            if (e.DropIndex < 0 && parent == st)
                                            {
                                                goto Skip;
                                            }

                                            foreach (var draggingItem in draggingItems)
                                            {
                                                var first = st.Nodes.FirstOrDefault();
                                                draggingItem.Server.Group = first != null ? first.Server.Group :
                                                        st.Name != I18NUtil.GetAppStringValue(@"EmptyGroup") ? st.Name :
                                                        string.Empty;
                                                draggingItem.Parent = st;
                                            }
                                            e.Cancel = false;
                                            return;
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            Skip:
            e.Cancel = true;
        }

        #endregion

        #region LinkTextBox

        private void LinkTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            GenQr(LinkTextBox.Text);
        }

        private void LinkTextBox_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var textBox = (TextBox)sender;
                textBox.Dispatcher?.BeginInvoke(new Action(() => { textBox.SelectAll(); }));
            }
        }

        private void LinkTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        #endregion

        private void ObfsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var obfs = (Obfs.ObfsBase)Obfs.ObfsFactory.GetObfs(ObfsComboBox.SelectedItem.ToString());
                var properties = obfs.GetObfs()[ObfsComboBox.SelectedItem.ToString()];
                ObfsParamTextBox.IsEnabled = properties[2] > 0;
            }
            catch
            {
                ObfsParamTextBox.IsEnabled = true;
            }
        }

        private bool SaveConfig()
        {
            string oldServerId = null;
            if (_modifiedConfiguration.index >= 0 && _modifiedConfiguration.index < _modifiedConfiguration.configs.Count)
            {
                oldServerId = _modifiedConfiguration.configs[_modifiedConfiguration.index].Id;
            }
            _modifiedConfiguration.configs.Clear();
            _modifiedConfiguration.configs.AddRange(ServerViewModel.ServerTreeViewModelToList(ServerViewModel.ServersTreeViewCollection));
            if (oldServerId != null)
            {
                var currentIndex = _modifiedConfiguration.configs.FindIndex(server => server.Id == oldServerId);
                if (currentIndex != -1)
                {
                    _modifiedConfiguration.index = currentIndex;
                }
            }

            if (_modifiedConfiguration.configs.Count == 0)
            {
                MessageBox.Show(this.GetWindowStringValue(@"NoServer"));
                return false;
            }

            _controller.SaveServersConfig(_modifiedConfiguration);
            return true;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ApplyButton.IsEnabled)
            {
                if (SaveConfig())
                {
                    Close();
                }
            }
            else
            {
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (SaveConfig())
            {
                ApplyButton.IsEnabled = false;
            }
        }

        #region QrCode

        private void GenQr(string text)
        {
            if (PictureQrCode.Visibility != Visibility.Visible)
            {
                return;
            }

            try
            {
                var h = Convert.ToInt32(MainGrid.ActualHeight);
                var w = Convert.ToInt32(MainGrid.ColumnDefinitions[2].ActualWidth - PictureQrCode.Margin.Left -
                                        PictureQrCode.Margin.Right);
                if (h <= 0 || w <= 0)
                {
                    PictureQrCode.Source = null;
                }
                else
                {
                    PictureQrCode.Source = text != string.Empty
                            ? QrCodeUtils.GenQrCode(text, w, h)
                            : QrCodeUtils.GenQrCode2(text, Math.Min(w, h));
                }
            }
            catch
            {
                PictureQrCode.Source = null;
            }
        }
        private double _oldWidth = 400.0;
        private void ShowQrCodeButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (PictureQrCode.Visibility != Visibility.Visible)
            {
                PictureQrCode.Visibility = Splitter2.Visibility = Visibility.Visible;
                MainGrid.ColumnDefinitions[2].Width = new GridLength(_oldWidth, GridUnitType.Pixel);
                Width += _oldWidth;
                ShowQrCodeButton.Content = @"<<";
            }
            else
            {
                PictureQrCode.Visibility = Splitter2.Visibility = Visibility.Collapsed;
                _oldWidth = MainGrid.ColumnDefinitions[2].ActualWidth;
                Width -= _oldWidth;
                MainGrid.ColumnDefinitions[2].Width = new GridLength(0);
                ShowQrCodeButton.Content = @">>";
            }
        }

        #endregion

        private void ConfigWindow_OnActivated(object sender, EventArgs e)
        {
            Topmost = false;
        }
    }
}
