using Shadowsocks.Controller;
using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Model;
using Shadowsocks.Util;
using Shadowsocks.ViewModel;
using System;
using System.Windows;

namespace Shadowsocks.View
{
    public partial class SettingsWindow
    {
        public SettingsWindow(MainController controller)
        {
            InitializeComponent();
            I18NUtil.SetLanguage(Resources, @"SettingsWindow");
            _controller = controller;
            Closed += (o, e) => { _controller.ConfigChanged -= controller_ConfigChanged; };
            _controller.ConfigChanged += controller_ConfigChanged;
            LoadCurrentConfiguration();
        }

        private readonly MainController _controller;

        public SettingViewModel SettingViewModel { get; set; } = new SettingViewModel();

        private void LoadCurrentConfiguration()
        {
            SettingViewModel.ReadConfig();
            SettingViewModel.ModifiedConfiguration.PropertyChanged += (o, args) =>
            {
                ApplyButton.IsEnabled = true;
            };
            AutoStartupCheckBox.IsChecked = AutoStartup.Check();
            Title = $@"{this.GetWindowStringValue(@"Title")}({(Global.GuiConfig.ShareOverLan ? this.GetWindowStringValue(@"Any") : this.GetWindowStringValue(@"Local"))}:{Global.GuiConfig.LocalPort} {this.GetWindowStringValue(@"Version")}:{UpdateChecker.FullVersion})";
            ApplyButton.IsEnabled = false;
        }

        private void controller_ConfigChanged(object sender, EventArgs e)
        {
            LoadCurrentConfiguration();
        }

        private void SaveConfig()
        {
            if (SettingViewModel.ModifiedConfiguration.LangName != Global.GuiConfig.LangName)
            {
                MessageBox.Show(this.GetWindowStringValue(@"RestartRequired"), UpdateChecker.Name, MessageBoxButton.OK);
            }
            _controller.SaveServersConfig(SettingViewModel.ModifiedConfiguration, true);
            var isAutoStartup = AutoStartupCheckBox.IsChecked.GetValueOrDefault();
            if (isAutoStartup != AutoStartup.Check()
            && !AutoStartup.Set(isAutoStartup))
            {
                MessageBox.Show(this.GetWindowStringValue(@"FailAutoStartUp"), UpdateChecker.Name, MessageBoxButton.OK);
            }
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
        }

        private void DefaultButton_Click(object sender, RoutedEventArgs e)
        {
            SettingViewModel.ModifiedConfiguration.ReconnectTimes = 4;
            SettingViewModel.ModifiedConfiguration.ConnectTimeout = SettingViewModel.ModifiedConfiguration.ProxyEnable ? 10 : 5;
            SettingViewModel.ModifiedConfiguration.Ttl = 60;
        }

        private void AutoStartupCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }
    }
}
