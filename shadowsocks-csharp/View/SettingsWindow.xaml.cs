using Shadowsocks.Controller;
using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Controls;
using Shadowsocks.Enums;
using Shadowsocks.Model;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Shadowsocks.View
{
    public partial class SettingsWindow
    {
        public SettingsWindow(MainController controller)
        {
            InitializeComponent();
            I18NUtil.SetLanguage(Resources, @"SettingsWindow");
            Closed += (o, e) => { _controller.ConfigChanged -= controller_ConfigChanged; };
            _controller = controller;
        }

        private void Window_Loaded(object sender, RoutedEventArgs _)
        {
            UpdateTitle();
            LoadItems();
            _controller.ConfigChanged += controller_ConfigChanged;
            foreach (var c in ViewUtils.FindVisualChildren<TextBox>(this))
            {
                //Not Child of NumberUpDown
                if (c.Name.EndsWith(@"TextBox"))
                {
                    c.TextChanged += (o, e) => { ApplyButton.IsEnabled = true; };
                }
            }
            foreach (var c in ViewUtils.FindVisualChildren<PasswordBox>(this))
            {
                c.PasswordChanged += (o, e) => { ApplyButton.IsEnabled = true; };
            }
            foreach (var c in ViewUtils.FindVisualChildren<CheckBox>(this))
            {
                c.Checked += (o, e) => { ApplyButton.IsEnabled = true; };
                c.Unchecked += (o, e) => { ApplyButton.IsEnabled = true; };
            }
            foreach (var c in ViewUtils.FindVisualChildren<ComboBox>(this))
            {
                c.SelectionChanged += (o, e) => { ApplyButton.IsEnabled = true; };
            }
            foreach (var c in ViewUtils.FindVisualChildren<NumberUpDown>(this))
            {
                c.ValueChanged += (o, e) => { ApplyButton.IsEnabled = true; };
            }

            //Fix Width
            foreach (var c in ViewUtils.FindVisualChildren<GroupBox>(this))
            {
                c.MaxWidth = c.ActualWidth;
                c.MinWidth = c.ActualWidth;
            }

            LoadCurrentConfiguration();
            ApplyButton.IsEnabled = false;
        }

        private readonly MainController _controller;
        private Configuration _modifiedConfiguration;
        private readonly List<string> _balanceIndexMap = new List<string>();

        private void UpdateTitle()
        {
            Title = $@"{this.GetWindowStringValue(@"Title")}({(Global.GuiConfig.ShareOverLan ? this.GetWindowStringValue(@"Any") : this.GetWindowStringValue(@"Local"))}:{Global.GuiConfig.LocalPort} {this.GetWindowStringValue(@"Version")}:{UpdateChecker.FullVersion})";
        }

        private void LoadItems()
        {
            ProxyTypeComboBox.Items.Add(this.GetWindowStringValue(@"Socks5"));
            ProxyTypeComboBox.Items.Add(this.GetWindowStringValue(@"Http"));
            ProxyTypeComboBox.Items.Add(this.GetWindowStringValue(@"TcpPortTunnel"));
            foreach (var value in Enum.GetValues(typeof(LoadBalance)))
            {
                var str = value.ToString();
                _balanceIndexMap.Add(str);
                BalanceComboBox.Items.Add(I18NUtil.GetAppStringValue(str));
            }
        }

        private bool SaveSettings()
        {
            try
            {
                Configuration.CheckPort(ProxyPortNumber.NumValue);
                _modifiedConfiguration.ShareOverLan = ShareOverLanCheckBox.IsChecked.GetValueOrDefault();
                _modifiedConfiguration.LocalPort = ProxyPortNumber.NumValue;
                _modifiedConfiguration.ReconnectTimes = ReconnectNumber.NumValue;

                if (AutoStartupCheckBox.IsChecked != AutoStartup.Check() && !AutoStartup.Set(AutoStartupCheckBox.IsChecked.GetValueOrDefault()))
                {
                    MessageBox.Show(this.GetWindowStringValue(@"FailAutoStartUp"));
                }

                _modifiedConfiguration.Random = BalanceCheckBox.IsChecked.GetValueOrDefault();
                _modifiedConfiguration.BalanceAlgorithm = BalanceComboBox.SelectedIndex >= 0 &&
                                                          BalanceComboBox.SelectedIndex < _balanceIndexMap.Count
                        ? _balanceIndexMap[BalanceComboBox.SelectedIndex]
                        : LoadBalance.LowException.ToString();
                _modifiedConfiguration.RandomInGroup = BalanceInGroupCheckBox.IsChecked.GetValueOrDefault();
                _modifiedConfiguration.Ttl = TtlNumber.NumValue;
                _modifiedConfiguration.ConnectTimeout = TimeoutNumber.NumValue;
                _modifiedConfiguration.DnsServer = DnsTextBox.Text;
                _modifiedConfiguration.LocalDnsServer = LocalDnsTextBox.Text;
                _modifiedConfiguration.ProxyEnable = SocksProxyCheckBox.IsChecked.GetValueOrDefault();
                _modifiedConfiguration.PacDirectGoProxy = PacProxyCheckBox.IsChecked.GetValueOrDefault();
                _modifiedConfiguration.ProxyType = ProxyTypeComboBox.SelectedIndex;
                _modifiedConfiguration.ProxyHost = SocksServerTextBox.Text;
                _modifiedConfiguration.ProxyPort = SocksPortTextBox.NumValue;
                _modifiedConfiguration.ProxyAuthUser = SocksUserTextBox.Text;
                _modifiedConfiguration.ProxyAuthPass = SocksPassPasswordBox.Password;
                _modifiedConfiguration.ProxyUserAgent = SocksUserAgentTextBox.Text;
                _modifiedConfiguration.AuthUser = AuthUserTextBox.Text;
                _modifiedConfiguration.AuthPass = AuthPassPasswordBox.Password;

                _modifiedConfiguration.AutoBan = AutoBanCheckBox.IsChecked.GetValueOrDefault();
                _modifiedConfiguration.CheckSwitchAutoCloseAll = SwitchAutoCloseAllCheckBox.IsChecked.GetValueOrDefault();
                _modifiedConfiguration.LogEnable = LogEnableCheckBox.IsChecked.GetValueOrDefault();

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            return false;
        }

        private void LoadSettings()
        {
            ShareOverLanCheckBox.IsChecked = _modifiedConfiguration.ShareOverLan;
            ProxyPortNumber.NumValue = _modifiedConfiguration.LocalPort;
            AuthUserTextBox.Text = _modifiedConfiguration.AuthUser;
            AuthPassPasswordBox.Password = _modifiedConfiguration.AuthPass;

            AutoStartupCheckBox.IsChecked = AutoStartup.Check();
            SwitchAutoCloseAllCheckBox.IsChecked = _modifiedConfiguration.CheckSwitchAutoCloseAll;
            BalanceCheckBox.IsChecked = _modifiedConfiguration.Random;
            var selectedIndex = 0;
            for (var i = 0; i < _balanceIndexMap.Count; ++i)
            {
                if (_modifiedConfiguration.BalanceAlgorithm == _balanceIndexMap[i])
                {
                    selectedIndex = i;
                    break;
                }
            }
            BalanceComboBox.SelectedIndex = selectedIndex;
            BalanceInGroupCheckBox.IsChecked = _modifiedConfiguration.RandomInGroup;
            AutoBanCheckBox.IsChecked = _modifiedConfiguration.AutoBan;
            LogEnableCheckBox.IsChecked = _modifiedConfiguration.LogEnable;

            DnsTextBox.Text = _modifiedConfiguration.DnsServer;
            LocalDnsTextBox.Text = _modifiedConfiguration.LocalDnsServer;
            ReconnectNumber.NumValue = _modifiedConfiguration.ReconnectTimes;
            TimeoutNumber.NumValue = _modifiedConfiguration.ConnectTimeout;
            TtlNumber.NumValue = _modifiedConfiguration.Ttl;

            SocksProxyCheckBox.IsChecked = _modifiedConfiguration.ProxyEnable;
            PacProxyCheckBox.IsChecked = _modifiedConfiguration.PacDirectGoProxy;
            ProxyTypeComboBox.SelectedIndex = _modifiedConfiguration.ProxyType;
            SocksServerTextBox.Text = _modifiedConfiguration.ProxyHost;
            SocksPortTextBox.NumValue = _modifiedConfiguration.ProxyPort;
            SocksUserTextBox.Text = _modifiedConfiguration.ProxyAuthUser;
            SocksPassPasswordBox.Password = _modifiedConfiguration.ProxyAuthPass;
            SocksUserAgentTextBox.Text = _modifiedConfiguration.ProxyUserAgent;
        }

        private void LoadCurrentConfiguration()
        {
            _modifiedConfiguration = Global.Load();
            LoadSettings();
        }

        private void controller_ConfigChanged(object sender, EventArgs e)
        {
            LoadCurrentConfiguration();
            UpdateTitle();
            ApplyButton.IsEnabled = false;
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

        private void DefaultButton_Click(object sender, RoutedEventArgs e)
        {
            ReconnectNumber.NumValue = 4;
            TimeoutNumber.NumValue = SocksProxyCheckBox.IsChecked == true ? 10 : 5;
            TtlNumber.NumValue = 60;
        }
    }
}
