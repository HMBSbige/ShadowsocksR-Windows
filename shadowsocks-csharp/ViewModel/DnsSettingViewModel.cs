using Shadowsocks.Enums;
using Shadowsocks.Model;
using Shadowsocks.Util.NetUtils;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Shadowsocks.ViewModel
{
    public class DnsSettingViewModel : ViewModelBase
    {
        public DnsSettingViewModel()
        {
            _currentClient = null;
            _clients = new ObservableCollection<DnsClient>();
        }

        #region Event

        public event EventHandler DnsClientsChanged;

        private void CurrentClient_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DnsClient.DnsType))
            {
                if (sender is DnsClient client)
                {
                    client.Port = client.DnsType switch
                    {
                        DnsType.Default when client.Port == DnsClient.DefaultTlsPort => DnsClient.DefaultPort,
                        DnsType.DnsOverTls when client.Port == DnsClient.DefaultPort => DnsClient.DefaultTlsPort,
                        _ => client.Port
                    };
                    client.DnsServer = client.DnsType switch
                    {
                        DnsType.Default when client.DnsServer == DnsClient.DefaultTlsDnsServer => DnsClient.DefaultDnsServer,
                        DnsType.DnsOverTls when client.DnsServer == DnsClient.DefaultDnsServer => DnsClient.DefaultTlsDnsServer,
                        _ => client.DnsServer
                    };
                }
            }
            DnsClientsChanged?.Invoke(sender, EventArgs.Empty);
        }

        private void Clients_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            DnsClientsChanged?.Invoke(sender, EventArgs.Empty);
        }

        #endregion

        private ObservableCollection<DnsClient> _clients;
        private DnsClient _currentClient;

        public ObservableCollection<DnsClient> Clients
        {
            get => _clients;
            set
            {
                if (SetField(ref _clients, value))
                {
                    DnsClientsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public DnsClient CurrentClient
        {
            get => _currentClient;
            set
            {
                if (SetField(ref _currentClient, value))
                {
                    if (_currentClient != null)
                    {
                        _currentClient.PropertyChanged -= CurrentClient_PropertyChanged;
                        _currentClient.PropertyChanged += CurrentClient_PropertyChanged;
                    }
                    OnPropertyChanged(nameof(IsCurrentClientNull));
                }
            }
        }

        public Visibility IsCurrentClientNull => CurrentClient == null ? Visibility.Hidden : Visibility.Visible;

        public void ReadConfig()
        {
            var config = Global.Load();
            Clients.Clear();
            foreach (var client in config.DnsClients)
            {
                Clients.Add(client);
            }
            CurrentClient = Clients.Count > 0 ? Clients[0] : null;
            Clients.CollectionChanged -= Clients_CollectionChanged;
            Clients.CollectionChanged += Clients_CollectionChanged;
        }

        public void SaveConfig()
        {
            Global.GuiConfig.DnsClients.Clear();
            foreach (var client in Clients)
            {
                Global.GuiConfig.DnsClients.Add(client);
            }
            DnsUtil.DnsBuffer.Clear();
            Global.Controller.SaveAndNotifyChanged();
        }

        public void AddNewDns()
        {
            var newDns = new DnsClient(DnsType.Default);
            Clients.Add(newDns);
            CurrentClient = newDns;
        }

        public void Delete()
        {
            if (CurrentClient != null)
            {
                Clients.Remove(CurrentClient);
                CurrentClient = Clients.LastOrDefault();
            }
        }
    }
}
