using Shadowsocks.Controller;
using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Controls;
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
        public SettingsWindow(ShadowsocksController controller)
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

        private readonly ShadowsocksController _controller;
        private Configuration _modifiedConfiguration;
        private readonly List<string> _balanceIndexMap = new List<string>();

        private void UpdateTitle()
        {
            Title = $@"{this.GetWindowStringValue(@"Title")}({(_controller.GetCurrentConfiguration().shareOverLan ? this.GetWindowStringValue(@"Any") : this.GetWindowStringValue(@"Local"))}:{_controller.GetCurrentConfiguration().localPort} {this.GetWindowStringValue(@"Version")}:{UpdateChecker.FullVersion})";
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
                _modifiedConfiguration.shareOverLan = ShareOverLanCheckBox.IsChecked.GetValueOrDefault();
                _modifiedConfiguration.localPort = ProxyPortNumber.NumValue;
                _modifiedConfiguration.reconnectTimes = ReconnectNumber.NumValue;

                if (AutoStartupCheckBox.IsChecked != AutoStartup.Check() && !AutoStartup.Set(AutoStartupCheckBox.IsChecked.GetValueOrDefault()))
                {
                    MessageBox.Show(this.GetWindowStringValue(@"FailAutoStartUp"));
                }

                _modifiedConfiguration.random = BalanceCheckBox.IsChecked.GetValueOrDefault();
                _modifiedConfiguration.balanceAlgorithm = BalanceComboBox.SelectedIndex >= 0 &&
                                                          BalanceComboBox.SelectedIndex < _balanceIndexMap.Count
                        ? _balanceIndexMap[BalanceComboBox.SelectedIndex]
                        : LoadBalance.LowException.ToString();
                _modifiedConfiguration.randomInGroup = BalanceInGroupCheckBox.IsChecked.GetValueOrDefault();
                _modifiedConfiguration.TTL = TtlNumber.NumValue;
                _modifiedConfiguration.connectTimeout = TimeoutNumber.NumValue;
                _modifiedConfiguration.dnsServer = DnsTextBox.Text;
                _modifiedConfiguration.localDnsServer = LocalDnsTextBox.Text;
                _modifiedConfiguration.proxyEnable = SocksProxyCheckBox.IsChecked.GetValueOrDefault();
                _modifiedConfiguration.pacDirectGoProxy = PacProxyCheckBox.IsChecked.GetValueOrDefault();
                _modifiedConfiguration.proxyType = ProxyTypeComboBox.SelectedIndex;
                _modifiedConfiguration.proxyHost = SocksServerTextBox.Text;
                _modifiedConfiguration.proxyPort = SocksPortTextBox.NumValue;
                _modifiedConfiguration.proxyAuthUser = SocksUserTextBox.Text;
                _modifiedConfiguration.proxyAuthPass = SocksPassPasswordBox.Password;
                _modifiedConfiguration.proxyUserAgent = SocksUserAgentTextBox.Text;
                _modifiedConfiguration.authUser = AuthUserTextBox.Text;
                _modifiedConfiguration.authPass = AuthPassPasswordBox.Password;

                _modifiedConfiguration.autoBan = AutoBanCheckBox.IsChecked.GetValueOrDefault();
                _modifiedConfiguration.checkSwitchAutoCloseAll = SwitchAutoCloseAllCheckBox.IsChecked.GetValueOrDefault();
                _modifiedConfiguration.logEnable = LogEnableCheckBox.IsChecked.GetValueOrDefault();

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
            ShareOverLanCheckBox.IsChecked = _modifiedConfiguration.shareOverLan;
            ProxyPortNumber.NumValue = _modifiedConfiguration.localPort;
            AuthUserTextBox.Text = _modifiedConfiguration.authUser;
            AuthPassPasswordBox.Password = _modifiedConfiguration.authPass;

            AutoStartupCheckBox.IsChecked = AutoStartup.Check();
            SwitchAutoCloseAllCheckBox.IsChecked = _modifiedConfiguration.checkSwitchAutoCloseAll;
            BalanceCheckBox.IsChecked = _modifiedConfiguration.random;
            var selectedIndex = 0;
            for (var i = 0; i < _balanceIndexMap.Count; ++i)
            {
                if (_modifiedConfiguration.balanceAlgorithm == _balanceIndexMap[i])
                {
                    selectedIndex = i;
                    break;
                }
            }
            BalanceComboBox.SelectedIndex = selectedIndex;
            BalanceInGroupCheckBox.IsChecked = _modifiedConfiguration.randomInGroup;
            AutoBanCheckBox.IsChecked = _modifiedConfiguration.autoBan;
            LogEnableCheckBox.IsChecked = _modifiedConfiguration.logEnable;

            DnsTextBox.Text = _modifiedConfiguration.dnsServer;
            LocalDnsTextBox.Text = _modifiedConfiguration.localDnsServer;
            ReconnectNumber.NumValue = _modifiedConfiguration.reconnectTimes;
            TimeoutNumber.NumValue = _modifiedConfiguration.connectTimeout;
            TtlNumber.NumValue = _modifiedConfiguration.TTL;

            SocksProxyCheckBox.IsChecked = _modifiedConfiguration.proxyEnable;
            PacProxyCheckBox.IsChecked = _modifiedConfiguration.pacDirectGoProxy;
            ProxyTypeComboBox.SelectedIndex = _modifiedConfiguration.proxyType;
            SocksServerTextBox.Text = _modifiedConfiguration.proxyHost;
            SocksPortTextBox.NumValue = _modifiedConfiguration.proxyPort;
            SocksUserTextBox.Text = _modifiedConfiguration.proxyAuthUser;
            SocksPassPasswordBox.Password = _modifiedConfiguration.proxyAuthPass;
            SocksUserAgentTextBox.Text = _modifiedConfiguration.proxyUserAgent;
        }

        private void LoadCurrentConfiguration()
        {
            _modifiedConfiguration = _controller.GetConfiguration();
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
