using Shadowsocks.Controller;
using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Encryption;
using Shadowsocks.Model;
using Shadowsocks.Util;
using Shadowsocks.ViewModel;
using System;
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
            ServerViewModel.TreeView = ServersTreeView;
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
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Loaded,
                        new Action(() => { MoveToSelectedItem(_focusIndex); }));
            }

            ServerGroupBox.Visibility = ServersTreeView.SelectedValue is Server ? Visibility.Visible : Visibility.Hidden;
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
            ServerViewModel.ReadConfig(_modifiedConfiguration);

            // Load all items
            ExpandTree(true);
            ExpandTree(false);

            if (scrollToSelectedItem)
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Loaded,
                        new Action(() => { MoveToSelectedItem(_modifiedConfiguration.index); }));
            }

            ApplyButton.IsEnabled = false;
        }

        #region TreeView

        public void MoveToSelectedItem(int index)
        {
            if (index >= 0 && index < _modifiedConfiguration.configs.Count)
            {
                var server = _modifiedConfiguration.configs[index];
                var serverTreeViewModel = ServerTreeViewModel.FindNode(ServerViewModel.ServersTreeViewCollection, server.Id);
                if (serverTreeViewModel != null)
                {
                    MoveToSelectedItem(serverTreeViewModel);
                }
            }
        }

        private void ExpandTree(bool expand)
        {
            if (expand)
            {
                foreach (var item in ServersTreeView.Items)
                {
                    var dependencyObject = ServersTreeView.ItemContainerGenerator.ContainerFromItem(item);
                    ((TreeViewItem)dependencyObject)?.ExpandSubtree();
                }
            }
            else
            {
                foreach (var treeViewItem in ViewUtils.FindVisualChildren<TreeViewItem>(ServersTreeView))
                {
                    treeViewItem.IsExpanded = false;
                }
            }
        }

        private void MoveToSelectedItem(ServerTreeViewModel serverTreeViewModel)
        {
            var ti = ViewUtils.GetTreeViewItem(ServersTreeView.ItemContainerGenerator, serverTreeViewModel);
            if (ti != null)
            {
                ti.IsSelected = true;
                ti.BringIntoView();
                ti.Focus();
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServersTreeView.SelectedItem is ServerTreeViewModel st)
            {
                switch (st.Type)
                {
                    case ServerTreeViewType.Subtag:
                        break;
                    case ServerTreeViewType.Group:
                    {
                        var item = new ServerTreeViewModel
                        { Type = ServerTreeViewType.Server, Server = new Server { Group = st.Name } };
                        st.Nodes.Add(item);
                        MoveToSelectedItem(item);
                        return;
                    }
                    case ServerTreeViewType.Server:
                    {
                        var parent = ViewUtils.GetTreeViewItemParent(ViewUtils.GetTreeViewItem(ServersTreeView.ItemContainerGenerator, st));
                        if (parent != null && parent.Header is ServerTreeViewModel parentSt)
                        {
                            var item = new ServerTreeViewModel { Type = ServerTreeViewType.Server, Server = Server.Clone(st.Server) };
                            var index = parentSt.Nodes.IndexOf(st) + 1;
                            parentSt.Nodes.Insert(index, item);
                            MoveToSelectedItem(item);
                        }
                        return;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            MessageBox.Show(this.GetWindowStringValue(@"AddServerError"), UpdateChecker.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServersTreeView.SelectedItem is ServerTreeViewModel st)
            {
                ServerTreeViewModel.Remove(ServerViewModel.ServersTreeViewCollection, st);
            }
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
            ServerViewModel.SelectedServer_ServerChanged();
            ServerGroupBox.Visibility = ServersTreeView.SelectedValue is Server ? Visibility.Visible : Visibility.Hidden;
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
            _modifiedConfiguration.configs.Clear();
            _modifiedConfiguration.configs.AddRange(ServerViewModel.ServerTreeViewModelToList(ServerViewModel.ServersTreeViewCollection));
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
    }
}
