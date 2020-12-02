using Shadowsocks.Controller;
using Shadowsocks.Encryption;
using Shadowsocks.Enums;
using Shadowsocks.Model;
using Shadowsocks.Util;
using Shadowsocks.ViewModel;
using Syncfusion.Data.Extensions;
using Syncfusion.UI.Xaml.TreeView;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Shadowsocks.View
{
    public partial class ServerConfigWindow
    {
        public ServerConfigWindow(MainController controller, int focusIndex)
        {
            InitializeComponent();
            I18NUtil.SetLanguage(Resources, @"ConfigWindow");
            SizeChanged += (o, args) => { GenQr(LinkTextBox.Text); };
            Splitter2.DragDelta += (o, args) => { GenQr(LinkTextBox.Text); };
            Closed += (o, e) =>
            {
                _controller.ConfigChanged -= controller_ConfigChanged;
                ServerConfigViewModel.ServersChanged -= ServerViewModel_ServersChanged;
            };

            _controller = controller;
            foreach (var name in from name in EncryptorFactory.RegisteredEncryptors.Keys let info = EncryptorFactory.GetEncryptorInfo(name) where info.Display select name)
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
            ServerConfigViewModel.ServersChanged += ServerViewModel_ServersChanged;
            _focusIndex = focusIndex;
            ServersTreeView_OnSelectionChanged(this, new ItemSelectionChangedEventArgs());
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

        private readonly MainController _controller;

        private Configuration _modifiedConfiguration;
        private int _focusIndex;

        public ServerConfigViewModel ServerConfigViewModel { get; set; } = new();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTitle();
            LoadCurrentConfiguration(false);
            switch (_focusIndex)
            {
                case -1:
                {
                    var index = _modifiedConfiguration.Index + 1;
                    if (index < 0 || index > _modifiedConfiguration.Configs.Count)
                    {
                        index = _modifiedConfiguration.Configs.Count;
                    }

                    _focusIndex = index;
                    break;
                }
                case -2:
                {
                    var index = _modifiedConfiguration.Index;
                    if (index < 0 || index > _modifiedConfiguration.Configs.Count)
                    {
                        index = _modifiedConfiguration.Configs.Count;
                    }

                    _focusIndex = index;
                    break;
                }
            }

            if (_focusIndex >= 0 && _focusIndex < _modifiedConfiguration.Configs.Count)
            {
                MoveToSelectedItem(_focusIndex);
            }
        }

        private void UpdateTitle()
        {
            Title = $@"{this.GetWindowStringValue(@"Title")}({(Global.GuiConfig.ShareOverLan ? this.GetWindowStringValue(@"Any") : this.GetWindowStringValue(@"Local"))}:{Global.GuiConfig.LocalPort} {this.GetWindowStringValue(@"Version")}:{Controller.HttpRequest.UpdateChecker.FullVersion})";
        }

        private void controller_ConfigChanged(object sender, EventArgs e)
        {
            LoadCurrentConfiguration(true);
            UpdateTitle();
        }

        private void LoadCurrentConfiguration(bool scrollToSelectedItem)
        {
            _modifiedConfiguration = Global.Load();
            ServerConfigViewModel.ReadServers(_modifiedConfiguration.Configs);

            ServersTreeView.ExpandAll();
            ServersTreeView.CollapseAll();

            if (scrollToSelectedItem)
            {
                MoveToSelectedItem(_modifiedConfiguration.Index);
            }

            ApplyButton.IsEnabled = false;
        }

        #region TreeView

        public void MoveToSelectedItem(int index)
        {
            if (index >= 0 && index < _modifiedConfiguration.Configs.Count)
            {
                var server = _modifiedConfiguration.Configs[index];
                MoveToSelectedItem(server.Id);
            }
        }

        private void MoveToSelectedItem(string id)
        {
            var serverTreeViewModel = ServerTreeViewModel.FindNode(ServerConfigViewModel.ServersTreeViewCollection, id);
            if (serverTreeViewModel != null)
            {
                MoveToSelectedItem(serverTreeViewModel);
            }
        }

        private void MoveToSelectedItem(ServerTreeViewModel serverTreeViewModel)
        {
            ServersTreeView.BringIntoView(serverTreeViewModel, false, true, ScrollToPosition.Center);
            ServersTreeView.SelectedItems?.Clear();
            ServersTreeView.SelectedItem = serverTreeViewModel;
            ServersTreeView_OnSelectionChanged(this, new ItemSelectionChangedEventArgs());
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServersTreeView.SelectedItem is ServerTreeViewModel st)
            {
                switch (st.Type)
                {
                    case ServerTreeViewType.Subtag:
                        MessageBox.Show(this.GetWindowStringValue(@"AddServerError"), Controller.HttpRequest.UpdateChecker.Name, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    case ServerTreeViewType.Group:
                    {
                        var item = new ServerTreeViewModel
                        {
                            Type = ServerTreeViewType.Server,
                            Server = new Server { Group = st.Name }
                        };
                        st.Nodes.Add(item);
                        MoveToSelectedItem(item);
                        return;
                    }
                    case ServerTreeViewType.Server:
                    {
                        var parent = ServerTreeViewModel.FindParentNode(ServerConfigViewModel.ServersTreeViewCollection, st);
                        if (parent != null)
                        {
                            var item = new ServerTreeViewModel
                            {
                                Type = ServerTreeViewType.Server,
                                Server = Server.Clone(st.Server)
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
            ServerConfigViewModel.ReadServers(new List<Server> { newServer });
            MoveToSelectedItem(newServer.Id);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var deleteItems = ServersTreeView.SelectedItems.ToArray();
            foreach (var selectedItem in deleteItems)
            {
                if (selectedItem is ServerTreeViewModel st)
                {
                    ServerTreeViewModel.Remove(ServerConfigViewModel.ServersTreeViewCollection, st);
                    ServersTreeView.SelectedItems.Clear();
                    ServersTreeView_OnSelectionChanged(this, new ItemSelectionChangedEventArgs());
                }
            }
        }

        private void ServersTreeView_OnSelectionChanged(object sender, ItemSelectionChangedEventArgs e)
        {
            if (ServersTreeView.SelectedItems is not null
                && ServersTreeView.SelectedItems.Count == 1
                && ServersTreeView.SelectedItem is ServerTreeViewModel { Type: ServerTreeViewType.Server })
            {
                ServerGroupBox.Visibility = Visibility.Visible;
            }
            else
            {
                ServerGroupBox.Visibility = Visibility.Hidden;
            }
        }

        private void ServersTreeView_OnItemDropping(object sender, TreeViewItemDroppingEventArgs e)
        {
            if (e.TargetNode.Content is ServerTreeViewModel target)
            {
                var source = e.DraggingNodes.Where(n => n.Content is ServerTreeViewModel).Select(n => (ServerTreeViewModel)n.Content).ToArray();
                if (!source.Any())
                {
                    goto Skip;
                }

                if (source.Any(st => st.Type == ServerTreeViewType.Subtag))
                {
                    if (ServerTreeViewModel.FindParentNode(ServerConfigViewModel.ServersTreeViewCollection, target) != null)
                    {
                        goto Skip;
                    }

                    var res = e.DraggingNodes.Where(n => n.Content is ServerTreeViewModel st && st.Type == ServerTreeViewType.Subtag).ToArray();
                    e.DraggingNodes.Clear();
                    res.ForEach(x => e.DraggingNodes.Add(x));

                    if (e.DropPosition == DropPosition.DropAsChild)
                    {
                        e.DropPosition = DropPosition.DropBelow;
                    }
                    return;
                }
                if (source.Any(st => st.Type == ServerTreeViewType.Group))
                {
                    var res = e.DraggingNodes.Where(n => n.Content is ServerTreeViewModel st && st.Type == ServerTreeViewType.Group).ToArray();
                    e.DraggingNodes.Clear();
                    res.ForEach(x => e.DraggingNodes.Add(x));

                    if (target.Type == ServerTreeViewType.Subtag)
                    {
                        goto Skip;
                    }
                    if (target.Type == ServerTreeViewType.Group)
                    {
                        var parent = ServerTreeViewModel.FindParentNode(ServerConfigViewModel.ServersTreeViewCollection, target);
                        if (parent == null)
                        {
                            goto Skip;
                        }

                        var isSameParent = e.DraggingNodes.All(n => n.Content is ServerTreeViewModel st
                           && ServerTreeViewModel.FindParentNode(ServerConfigViewModel.ServersTreeViewCollection, st) == parent);

                        if (!isSameParent)
                        {
                            goto Skip;
                        }

                        if (e.DropPosition == DropPosition.DropAsChild)
                        {
                            e.DropPosition = DropPosition.DropBelow;
                        }
                        return;
                    }
                    goto Skip;
                }
                // all is servers
                if (target.Type == ServerTreeViewType.Subtag)
                {
                    goto Skip;
                }
                if (target.Type == ServerTreeViewType.Group)
                {
                    var sub = ServerTreeViewModel.FindParentNode(ServerConfigViewModel.ServersTreeViewCollection, target);
                    if (sub == null)
                    {
                        return;
                    }

                    var subName = sub.Name == I18NUtil.GetAppStringValue(@"EmptySubtag") ? string.Empty : sub.Name;
                    var groupName = target.Name == I18NUtil.GetAppStringValue(@"EmptyGroup") ? string.Empty : target.Name;

                    e.DraggingNodes.ForEach(n =>
                    {
                        var server = ((ServerTreeViewModel)n.Content).Server;
                        server.Group = groupName;
                        server.SubTag = subName;
                    });

                    e.DropPosition = DropPosition.DropAsChild;
                    return;
                }

                e.DraggingNodes.ForEach(n =>
                {
                    var server = ((ServerTreeViewModel)n.Content).Server;
                    server.Group = target.Server.Group;
                    server.SubTag = target.Server.SubTag;
                });

                if (e.DropPosition == DropPosition.DropAsChild)
                {
                    e.DropPosition = DropPosition.DropBelow;
                }
                return;
            }
Skip:
            e.Handled = true;
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
                textBox.Dispatcher?.InvokeAsync(() => { textBox.SelectAll(); });
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
                if (ObfsComboBox.SelectedItem == null)
                {
                    return;
                }
                var obfs = (Obfs.ObfsBase)Obfs.ObfsFactory.GetObfs(ObfsComboBox.SelectedItem.ToString());
                var properties = obfs.GetObfs()[$@"{ObfsComboBox.SelectedItem}"];
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
            if (_modifiedConfiguration.Index >= 0 && _modifiedConfiguration.Index < _modifiedConfiguration.Configs.Count)
            {
                oldServerId = _modifiedConfiguration.Configs[_modifiedConfiguration.Index].Id;
            }
            _modifiedConfiguration.Configs.Clear();
            _modifiedConfiguration.Configs.AddRange(ServerConfigViewModel.ServerTreeViewModelToList(ServerConfigViewModel.ServersTreeViewCollection));
            if (oldServerId != null)
            {
                var currentIndex = _modifiedConfiguration.Configs.FindIndex(server => server.Id == oldServerId);
                if (currentIndex != -1)
                {
                    _modifiedConfiguration.Index = currentIndex;
                }
            }

            if (_modifiedConfiguration.Configs.Count == 0)
            {
                MessageBox.Show(this.GetWindowStringValue(@"NoServer"));
                return false;
            }

            _controller.SaveServersConfig(_modifiedConfiguration, true);
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
