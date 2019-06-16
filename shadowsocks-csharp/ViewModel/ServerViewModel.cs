using Shadowsocks.Model;
using System.Collections.ObjectModel;

namespace Shadowsocks.ViewModel
{
    public class ServerViewModel : ViewModelBase
    {
        public ServerViewModel()
        {
            ServerCollection = new ObservableCollection<ServerObject>();
            IsSsr = true;
        }

        public void ReadConfig(Configuration config)
        {
            ServerCollection.Clear();
            foreach (var server in config.configs)
            {
                var serverObject = ServerObject.CopyFromServer(server);

                serverObject.Enable = server.enable;
                serverObject.Protocoldata = server.getProtocolData();
                serverObject.Obfsdata = server.getObfsData();

                ServerCollection.Add(serverObject);
            }

            if (config.index >= 0 && config.index < ServerCollection.Count)
            {
                SelectedServer = ServerCollection[config.index];
            }
        }

        private ServerObject _selectedServer;
        public ServerObject SelectedServer
        {
            get => _selectedServer;
            set
            {
                if (_selectedServer != value)
                {
                    _selectedServer = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SsLink));
                    if (_selectedServer != null)
                    {
                        _selectedServer.PropertyChanged -= _selectedServer_PropertyChanged;
                        _selectedServer.PropertyChanged += _selectedServer_PropertyChanged;
                    }
                }
            }
        }

        private void _selectedServer_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(SsLink));
        }

        private ObservableCollection<ServerObject> _serverCollection;
        public ObservableCollection<ServerObject> ServerCollection
        {
            get => _serverCollection;
            set
            {
                if (_serverCollection != value)
                {
                    _serverCollection = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isSsr;
        public bool IsSsr
        {
            get => _isSsr;
            set
            {
                if (_isSsr != value)
                {
                    _isSsr = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SsLink));
                }
            }
        }

        public string SsLink
        {
            get
            {
                if (SelectedServer != null)
                {
                    return IsSsr ? SelectedServer.SsrLink : SelectedServer.SsLink;
                }
                return string.Empty;
            }
        }
    }
}
