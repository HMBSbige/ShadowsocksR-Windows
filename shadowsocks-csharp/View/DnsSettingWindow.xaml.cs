using Shadowsocks.Util;
using Shadowsocks.ViewModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Shadowsocks.View
{
    public partial class DnsSettingWindow
    {
        public DnsSettingWindow()
        {
            InitializeComponent();
            I18NUtil.SetLanguage(Resources, @"DnsSettingWindow");
            Closed += (o, e) =>
            {
                DnsSettingViewModel.DnsClientsChanged -= DnsSettingViewModel_DnsClientsChanged;
            };
            DnsSettingViewModel.DnsClientsChanged += DnsSettingViewModel_DnsClientsChanged;
            LoadConfig();
        }

        private void DnsSettingViewModel_DnsClientsChanged(object sender, System.EventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        public DnsSettingViewModel DnsSettingViewModel { get; set; } = new();

        private void LoadConfig()
        {
            DnsSettingViewModel.ReadConfig();
            ApplyButton.IsEnabled = false;
        }

        private void SaveConfig()
        {
            if (ApplyButton.IsEnabled)
            {
                DnsSettingViewModel.SaveConfig();
            }
        }

        private void AddButton_OnClick(object sender, RoutedEventArgs e)
        {
            DnsSettingViewModel.AddNewDns();
        }

        private void DeleteButton_OnClick(object sender, RoutedEventArgs e)
        {
            DnsSettingViewModel.Delete();
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
        {
            SaveConfig();
            Close();
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ApplyButton_OnClick(object sender, RoutedEventArgs e)
        {
            SaveConfig();
            LoadConfig();
        }

        private void TestButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DnsSettingViewModel.CurrentClient != null)
            {
                var client = DnsSettingViewModel.CurrentClient;
                var domain = DomainTextBox.Text;
                button.IsEnabled = false;
                AnswerTextBox.Text = string.Empty;
                Task.Run(async () =>
                {
                    var res = await client.QueryIpAddressAsync(domain, default);
                    Dispatcher?.InvokeAsync(() => { AnswerTextBox.Text = $@"{res}"; });
                }).ContinueWith(task =>
                {
                    Dispatcher?.InvokeAsync(() => { button.IsEnabled = true; });
                });
            }
        }
    }
}
