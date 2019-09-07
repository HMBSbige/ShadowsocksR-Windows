using Shadowsocks.Controller;
using Shadowsocks.Model;
using Shadowsocks.Util;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Shadowsocks.View
{
    public partial class SubscribeWindow
    {
        public SubscribeWindow(ShadowsocksController controller)
        {
            InitializeComponent();
            I18NUtil.SetLanguage(Resources, @"SubscribeWindow");
            Closed += (o, e) => { controller.ConfigChanged -= controller_ConfigChanged; };
            _controller = controller;
        }

        private readonly ShadowsocksController _controller;
        private Configuration _modifiedConfiguration;
        private int _oldSelectIndex;

        private void Window_Loaded(object sender, RoutedEventArgs _)
        {
            Title = this.GetWindowStringValue(@"Title");
            _controller.ConfigChanged += controller_ConfigChanged;

            UrlTextBox.TextChanged += (o, e) =>
            {
                if (UrlTextBox.IsFocused)
                {
                    ApplyButton.IsEnabled = true;
                }
            };
            AutoUpdateCheckBox.Checked += (o, e) => { ApplyButton.IsEnabled = true; };
            AutoUpdateCheckBox.Unchecked += (o, e) => { ApplyButton.IsEnabled = true; };

            LoadCurrentConfiguration();
            ApplyButton.IsEnabled = false;
        }

        private void controller_ConfigChanged(object sender, EventArgs e)
        {
            LoadCurrentConfiguration();
            ApplyButton.IsEnabled = false;
        }

        private void LoadCurrentConfiguration()
        {
            _modifiedConfiguration = _controller.GetConfiguration();
            LoadAllSettings();
            UrlTextBox.IsEnabled = ServerSubscribeListBox.Items.Count != 0;
        }

        private void LoadAllSettings()
        {
            const int selectIndex = 0;
            AutoUpdateCheckBox.IsChecked = _modifiedConfiguration.nodeFeedAutoUpdate;
            UpdateList();
            UpdateSelected(selectIndex);
            SetSelectIndex(selectIndex);
        }

        private void UpdateList()
        {
            ServerSubscribeListBox.Items.Clear();
            foreach (var ss in _modifiedConfiguration.serverSubscribes)
            {
                ServerSubscribeListBox.Items.Add($@"{(string.IsNullOrEmpty(ss.Group) ? @"    " : ss.Group + @" - ")}{ss.Url}");
            }
        }

        private void UpdateSelected(int index)
        {
            if (index >= 0 && index < _modifiedConfiguration.serverSubscribes.Count)
            {
                var ss = _modifiedConfiguration.serverSubscribes[index];
                UrlTextBox.Text = ss.Url;
                GroupTextBox.Text = ss.Group;
                _oldSelectIndex = index;
                if (ss.LastUpdateTime != 0)
                {
                    var now = new DateTime(1970, 1, 1, 0, 0, 0);
                    now = now.AddSeconds(ss.LastUpdateTime);
                    UpdateTextBox.Text = $@"{now.ToLongDateString()} {now.ToLongTimeString()}";
                }
                else
                {
                    UpdateTextBox.Text = @"(｢･ω･)｢";
                }
            }
        }

        private void SetSelectIndex(int index)
        {
            if (index >= 0 && index < _modifiedConfiguration.serverSubscribes.Count)
            {
                ServerSubscribeListBox.SelectedIndex = index;
            }
        }

        private void SaveSelected(int index)
        {
            if (index >= 0 && index < _modifiedConfiguration.serverSubscribes.Count)
            {
                var ss = _modifiedConfiguration.serverSubscribes[index];
                if (ss.Url != UrlTextBox.Text)
                {
                    ss.Url = UrlTextBox.Text;
                    ss.Group = string.Empty;
                    ss.LastUpdateTime = 0;
                }
            }
        }

        private void ServerSubscribeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectIndex = ServerSubscribeListBox.SelectedIndex;
            if (selectIndex == -1 || _oldSelectIndex == selectIndex)
            {
                return;
            }

            SaveSelected(_oldSelectIndex);
            UpdateList();
            UpdateSelected(selectIndex);
            SetSelectIndex(selectIndex);
        }

        private bool SaveSettings()
        {
            SaveSelected(ServerSubscribeListBox.SelectedIndex);
            _modifiedConfiguration.nodeFeedAutoUpdate = AutoUpdateCheckBox.IsChecked.GetValueOrDefault();
            return true;
        }

        private bool SaveConfig()
        {
            if (SaveSettings())
            {
                _controller.SaveServersConfig(_modifiedConfiguration);
                return true;
            }
            return false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ApplyButton.IsEnabled)
            {
                SaveConfig();
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
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSelected(_oldSelectIndex);
            var selectIndex = _modifiedConfiguration.serverSubscribes.Count;
            if (_oldSelectIndex >= 0 && _oldSelectIndex < _modifiedConfiguration.serverSubscribes.Count)
            {
                _modifiedConfiguration.serverSubscribes.Insert(selectIndex, new ServerSubscribe());
            }
            else
            {
                _modifiedConfiguration.serverSubscribes.Add(new ServerSubscribe());
            }

            UpdateList();
            UpdateSelected(selectIndex);
            SetSelectIndex(selectIndex);

            UrlTextBox.IsEnabled = true;

            ApplyButton.IsEnabled = true;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var selectIndex = ServerSubscribeListBox.SelectedIndex;
            if (selectIndex >= 0 && selectIndex < _modifiedConfiguration.serverSubscribes.Count)
            {
                _modifiedConfiguration.serverSubscribes.RemoveAt(selectIndex);
                if (selectIndex >= _modifiedConfiguration.serverSubscribes.Count)
                {
                    selectIndex = _modifiedConfiguration.serverSubscribes.Count - 1;
                }

                UpdateList();
                UpdateSelected(selectIndex);
                SetSelectIndex(selectIndex);
                ApplyButton.IsEnabled = true;
            }

            if (ServerSubscribeListBox.Items.Count == 0)
            {
                UrlTextBox.IsEnabled = false;
            }
        }
    }
}
