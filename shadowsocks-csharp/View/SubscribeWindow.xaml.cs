using Shadowsocks.Controller;
using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Controller.Service;
using Shadowsocks.Model;
using Shadowsocks.Util;
using Shadowsocks.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Shadowsocks.View
{
    public partial class SubscribeWindow
    {
        public SubscribeWindow(ShadowsocksController controller, UpdateSubscribeManager updateSubscribeManager, UpdateNode updateNodeChecker)
        {
            InitializeComponent();
            I18NUtil.SetLanguage(Resources, @"SubscribeWindow");
            Closed += (o, e) =>
            {
                controller.ConfigChanged -= controller_ConfigChanged;
                SubscribeWindowViewModel.SubscribesChanged -= SubscribeWindowViewModel_SubscribesChanged;
            };
            _controller = controller;
            _updateSubscribeManager = updateSubscribeManager;
            _updateNodeChecker = updateNodeChecker;
            _controller.ConfigChanged += controller_ConfigChanged;
            LoadCurrentConfiguration();
            SubscribeWindowViewModel.SubscribesChanged += SubscribeWindowViewModel_SubscribesChanged;
        }

        private void SubscribeWindowViewModel_SubscribesChanged(object sender, EventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        private readonly ShadowsocksController _controller;
        private readonly UpdateNode _updateNodeChecker;
        private readonly UpdateSubscribeManager _updateSubscribeManager;
        private Configuration _modifiedConfiguration;

        public SubscribeWindowViewModel SubscribeWindowViewModel { get; set; } = new SubscribeWindowViewModel();

        private void Window_Loaded(object sender, RoutedEventArgs _)
        {
            InfoGrid.Visibility = ServerSubscribeListBox.SelectedIndex == -1 ? Visibility.Hidden : Visibility.Visible;
        }

        private void controller_ConfigChanged(object sender, EventArgs e)
        {
            LoadCurrentConfiguration();
        }

        private void LoadCurrentConfiguration()
        {
            _modifiedConfiguration = _controller.GetConfiguration();
            SubscribeWindowViewModel.ReadConfig(_modifiedConfiguration);

            ApplyButton.IsEnabled = false;
        }

        private void ServerSubscribeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InfoGrid.Visibility = ServerSubscribeListBox.SelectedIndex == -1 ? Visibility.Hidden : Visibility.Visible;
        }

        private void DeleteUnusedServer()
        {
            _modifiedConfiguration.configs.RemoveAll(server =>
                    !string.IsNullOrEmpty(server.SubTag)
                    && _modifiedConfiguration.serverSubscribes.All(subscribe => subscribe.Tag != server.SubTag));
        }

        private bool SaveConfig()
        {
            var remarks = new HashSet<string>();
            foreach (var serverSubscribe in SubscribeWindowViewModel.SubscribeCollection)
            {
                if (remarks.Contains(serverSubscribe.Tag))
                {
                    return false;
                }
                remarks.Add(serverSubscribe.Tag);
            }
            _modifiedConfiguration.serverSubscribes.Clear();
            _modifiedConfiguration.serverSubscribes.AddRange(SubscribeWindowViewModel.SubscribeCollection);

            if (_modifiedConfiguration.configs.Any(server =>
            !string.IsNullOrEmpty(server.SubTag)
            && _modifiedConfiguration.serverSubscribes.All(subscribe => subscribe.Tag != server.SubTag)))
            {
                if (MessageBox.Show(this.GetWindowStringValue(@"SaveQuestion"),
                        UpdateChecker.Name, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes)
                        == MessageBoxResult.Yes)
                {
                    DeleteUnusedServer();
                }
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
                else
                {
                    SaveError();
                    return;
                }
            }
            Close();
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
                return;
            }
            SaveError();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            SubscribeWindowViewModel.SubscribeCollection.Add(new ServerSubscribe());
            SetServerListSelectedIndex(SubscribeWindowViewModel.SubscribeCollection.Count - 1);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var index = ServerSubscribeListBox.SelectedIndex;
            if (ServerSubscribeListBox.SelectedItem is ServerSubscribe serverSubscribe)
            {
                SubscribeWindowViewModel.SubscribeCollection.Remove(serverSubscribe);
            }
            SetServerListSelectedIndex(index);
        }

        private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
        {
            var index = ServerSubscribeListBox.SelectedIndex;
            if (ServerSubscribeListBox.SelectedItem is ServerSubscribe serverSubscribe)
            {
                var tag = serverSubscribe.Tag;
                _modifiedConfiguration.configs = _modifiedConfiguration.configs.Where(server => server.SubTag != tag).ToList();
                SubscribeWindowViewModel.SubscribeCollection.Remove(serverSubscribe);
            }
            SetServerListSelectedIndex(index);
        }

        private void SetServerListSelectedIndex(int index)
        {
            if (index < 0)
            {
                return;
            }

            if (index < ServerSubscribeListBox.Items.Count)
            {
                ServerSubscribeListBox.SelectedIndex = index;
                ServerSubscribeListBox.ScrollIntoView(ServerSubscribeListBox.Items[index]);
            }
            else
            {
                ServerSubscribeListBox.SelectedIndex = ServerSubscribeListBox.Items.Count - 1;
                if (ServerSubscribeListBox.SelectedIndex > 0)
                {
                    ServerSubscribeListBox.ScrollIntoView(ServerSubscribeListBox.Items[ServerSubscribeListBox.Items.Count - 1]);
                }
            }
        }

        private bool Save()
        {
            if (ApplyButton.IsEnabled)
            {
                return SaveConfig();
            }
            return true;
        }

        private void SaveError()
        {
            MessageBox.Show(this.GetWindowStringValue(@"SaveError"), UpdateChecker.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void UpdateButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (ServerSubscribeListBox.SelectedItem is ServerSubscribe serverSubscribe)
            {
                if (Save())
                {
                    ApplyButton.IsEnabled = false;
                    _updateSubscribeManager.CreateTask(_modifiedConfiguration, _updateNodeChecker, true, serverSubscribe);
                }
                else
                {
                    SaveError();
                }
            }
        }
    }
}
