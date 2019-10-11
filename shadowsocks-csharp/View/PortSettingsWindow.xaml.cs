using Shadowsocks.Controller;
using Shadowsocks.Controls;
using Shadowsocks.Model;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Shadowsocks.View
{
    public partial class PortSettingsWindow
    {
        private readonly ShadowsocksController _controller;
        private Configuration _modifiedConfiguration;
        private int _oldSelectedIndex = -1;

        private event EventHandler ValueChanged;

        public PortSettingsWindow(ShadowsocksController controller)
        {
            InitializeComponent();
            I18NUtil.SetLanguage(Resources, @"PortSettingsWindow");
            Closed += (o, e) => { _controller.ConfigChanged -= controller_ConfigChanged; };
            _controller = controller;
            LoadItems();
        }

        private void Window_Loaded(object sender, RoutedEventArgs _)
        {
            _controller.ConfigChanged += controller_ConfigChanged;
            ValueChanged += PortSettingsWindow_ValueChanged;

            foreach (var c in ViewUtils.FindVisualChildren<TextBox>(this))
            {
                //Not Child of NumberUpDown
                if (c.Name.EndsWith(@"TextBox"))
                {
                    c.TextChanged += (o, e) =>
                    {
                        ValueChanged?.Invoke(this, new EventArgs());
                    };
                }
            }
            foreach (var c in ViewUtils.FindVisualChildren<CheckBox>(this))
            {
                c.Checked += (o, e) =>
                {
                    ValueChanged?.Invoke(this, new EventArgs());
                };
                c.Unchecked += (o, e) =>
                {
                    ValueChanged?.Invoke(this, new EventArgs());
                };
            }
            foreach (var c in ViewUtils.FindVisualChildren<ComboBox>(this))
            {
                c.SelectionChanged += (o, e) =>
                {
                    ValueChanged?.Invoke(this, new EventArgs());
                };
            }
            foreach (var c in ViewUtils.FindVisualChildren<NumberUpDown>(this))
            {
                c.ValueChanged += (o, e) =>
                {
                    ValueChanged?.Invoke(this, new EventArgs());
                };
            }

            LoadCurrentConfiguration();
            ApplyButton.IsEnabled = false;
        }

        private void PortSettingsWindow_ValueChanged(object sender, EventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        private void LoadItems()
        {
            var items = new[]
            {
                    new {Text = this.GetWindowStringValue(@"PortForward"), Value = PortMapType.Forward},
                    new {Text = this.GetWindowStringValue(@"ForceProxy"), Value = PortMapType.ForceProxy},
                    new {Text = this.GetWindowStringValue(@"ProxyWithRule"), Value = PortMapType.RuleProxy}
            };
            TypeComboBox.ItemsSource = items;
        }

        private void controller_ConfigChanged(object sender, EventArgs e)
        {
            LoadCurrentConfiguration();
            ApplyButton.IsEnabled = false;
        }

        private void LoadCurrentConfiguration()
        {
            _modifiedConfiguration = _controller.GetConfiguration();
            LoadConfiguration(_modifiedConfiguration);
            LoadSelectedServer();
        }

        private void LoadConfiguration(Configuration configuration)
        {
            ServersComboBox.Items.Clear();
            ServersComboBox.Items.Add(string.Empty);
            var serverGroup = new Dictionary<string, int>();
            foreach (var s in configuration.configs)
            {
                if (!string.IsNullOrEmpty(s.Group) && !serverGroup.ContainsKey(s.Group))
                {
                    ServersComboBox.Items.Add(@"#" + s.Group);
                    serverGroup[s.Group] = 1;
                }
            }

            foreach (var s in configuration.configs)
            {
                ServersComboBox.Items.Add(GetDisplayText(s));
            }

            PortsListBox.Items.Clear();
            var list = new int[configuration.portMap.Count];
            var listIndex = 0;
            foreach (var it in configuration.portMap)
            {
                int.TryParse(it.Key, out list[listIndex]);

                listIndex += 1;
            }

            Array.Sort(list);
            foreach (var port in list)
            {
                var remarks = configuration.portMap[port.ToString()].remarks ?? string.Empty;
                PortsListBox.Items.Add(port + "    " + remarks);
            }

            _oldSelectedIndex = -1;
            if (PortsListBox.Items.Count > 0)
            {
                PortsListBox.SelectedIndex = 0;
            }
        }

        private void SaveSelectedServer()
        {
            if (_oldSelectedIndex != -1)
            {
                var refreshList = false;
                var key = _oldSelectedIndex.ToString();
                if (key != LocalPortNumber.NumValue.ToString())
                {
                    if (_modifiedConfiguration.portMap.ContainsKey(key))
                    {
                        _modifiedConfiguration.portMap.Remove(key);
                    }
                    refreshList = true;
                    key = LocalPortNumber.NumValue.ToString();
                    if (!int.TryParse(key, out _oldSelectedIndex))
                    {
                        _oldSelectedIndex = 0;
                    }
                }
                if (!_modifiedConfiguration.portMap.ContainsKey(key))
                {
                    _modifiedConfiguration.portMap[key] = new PortMapConfig();
                }
                var cfg = _modifiedConfiguration.portMap[key];

                cfg.enable = EnableCheckBox.IsChecked.GetValueOrDefault();
                cfg.type = (PortMapType)TypeComboBox.SelectedValue;
                cfg.id = GetId(ServersComboBox.Text);
                cfg.server_addr = TargetAddressTextBox.Text;
                if (cfg.remarks != RemarksTextBox.Text)
                {
                    refreshList = true;
                }
                cfg.remarks = RemarksTextBox.Text;
                cfg.server_port = TargetPortNumber.NumValue;
                if (refreshList)
                {
                    LoadConfiguration(_modifiedConfiguration);
                }
            }
        }

        private string GetIdText(string id)
        {
            foreach (var s in _modifiedConfiguration.configs)
            {
                if (id == s.Id)
                {
                    return GetDisplayText(s);
                }
            }

            return string.Empty;
        }

        private void LoadSelectedServer()
        {
            var key = ServerListText2Key((string)PortsListBox.SelectedItem);
            var serverGroup = new Dictionary<string, int>();
            foreach (var s in _modifiedConfiguration.configs)
            {
                if (!string.IsNullOrEmpty(s.Group) && !serverGroup.ContainsKey(s.Group))
                {
                    serverGroup[s.Group] = 1;
                }
            }
            if (key != null && _modifiedConfiguration.portMap.ContainsKey(key))
            {
                var cfg = _modifiedConfiguration.portMap[key];

                EnableCheckBox.IsChecked = cfg.enable;
                TypeComboBox.SelectedValue = cfg.type;
                var text = GetIdText(cfg.id);
                if (text.Length == 0 && serverGroup.ContainsKey(cfg.id))
                {
                    text = $@"#{cfg.id}";
                }
                ServersComboBox.Text = text;
                LocalPortNumber.NumValue = int.Parse(key);
                TargetAddressTextBox.Text = cfg.server_addr;
                TargetPortNumber.NumValue = cfg.server_port;
                RemarksTextBox.Text = cfg.remarks ?? string.Empty;

                if (!int.TryParse(key, out _oldSelectedIndex))
                {
                    _oldSelectedIndex = 0;
                }
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var hasBind = false;
            if (ValueChanged != null)
            {
                ValueChanged -= PortSettingsWindow_ValueChanged;
                hasBind = true;
            }

            SaveSelectedServer();
            const string key = @"0";
            if (!_modifiedConfiguration.portMap.ContainsKey(key))
            {
                _modifiedConfiguration.portMap[key] = new PortMapConfig();
                PortSettingsWindow_ValueChanged(this, new EventArgs());
            }

            var cfg = _modifiedConfiguration.portMap[key];

            cfg.enable = EnableCheckBox.IsChecked.GetValueOrDefault();
            cfg.type = (PortMapType)TypeComboBox.SelectedValue;
            cfg.id = GetId(ServersComboBox.Text);
            cfg.server_addr = TargetAddressTextBox.Text;
            cfg.remarks = RemarksTextBox.Text;
            cfg.server_port = TargetPortNumber.NumValue;

            _oldSelectedIndex = -1;
            LoadConfiguration(_modifiedConfiguration);
            LoadSelectedServer();

            if (hasBind)
            {
                ValueChanged += PortSettingsWindow_ValueChanged;
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var key = _oldSelectedIndex.ToString();
            if (_modifiedConfiguration.portMap.ContainsKey(key))
            {
                _modifiedConfiguration.portMap.Remove(key);
            }
            _oldSelectedIndex = -1;
            LoadConfiguration(_modifiedConfiguration);
            LoadSelectedServer();
            ValueChanged?.Invoke(this, new EventArgs());
        }

        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TypeComboBox.SelectedIndex == 0)
            {
                TargetAddressTextBox.IsReadOnly = false;
                TargetPortNumber.IsEnabled = true;
            }
            else
            {
                TargetAddressTextBox.IsReadOnly = true;
                TargetPortNumber.IsEnabled = false;
            }
        }

        private void PortsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var hasBind = false;
            if (ValueChanged != null)
            {
                ValueChanged -= PortSettingsWindow_ValueChanged;
                hasBind = true;
            }

            if (PortsListBox.SelectedIndex != -1)
            {
                SaveSelectedServer();
                LoadSelectedServer();
            }

            if (hasBind)
            {
                ValueChanged += PortSettingsWindow_ValueChanged;
            }
        }

        private void SaveConfig()
        {
            SaveSelectedServer();
            _controller.SaveServersPortMap(_modifiedConfiguration);
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
            SaveConfig();
            ApplyButton.IsEnabled = false;
        }

        #region Static

        private static string GetDisplayText(Server s)
        {
            return (!string.IsNullOrEmpty(s.Group) ? s.Group + " - " : "    - ") + s.FriendlyName + "        #" + s.Id;
        }

        private static string GetId(string text)
        {
            if (text.IndexOf('#') >= 0)
            {
                return text.Substring(text.IndexOf('#') + 1);
            }

            return text;
        }

        private static string ServerListText2Key(string text)
        {
            if (text != null)
            {
                var pos = text.IndexOf(' ');
                if (pos > 0)
                    return text.Substring(0, pos);
            }

            return text;
        }

        #endregion
    }
}
