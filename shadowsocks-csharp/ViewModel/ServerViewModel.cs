using Shadowsocks.Model;
using System;
using System.Collections.ObjectModel;

namespace Shadowsocks.ViewModel
{
    public class ServerViewModel : ViewModelBase
    {
        public ServerViewModel()
        {
            ServerCollection = new ObservableCollection<Server>();
            IsSsr = true;
        }

        public void ReadConfig(Configuration config)
        {
            ServerCollection = config.configs;
            if (config.index >= 0 && config.index < ServerCollection.Count)
            {
                SelectedServer = ServerCollection[config.index];
            }

            ServerCollection.CollectionChanged -= ServerCollection_CollectionChanged;
            ServerCollection.CollectionChanged += ServerCollection_CollectionChanged;
        }

        private void ServerCollection_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ServersChanged?.Invoke(this, new EventArgs());
        }

        public event EventHandler ServersChanged;

        private Server _selectedServer;
        public Server SelectedServer
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
                        _selectedServer.ServerChanged -= _selectedServer_ServerChanged;
                        _selectedServer.ServerChanged += _selectedServer_ServerChanged;
                    }
                }
            }
        }

        private void _selectedServer_ServerChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(SsLink));
            ServersChanged?.Invoke(this, new EventArgs());
        }

        private ObservableCollection<Server> _serverCollection;
        public ObservableCollection<Server> ServerCollection
        {
            get => _serverCollection;
            set
            {
                if (_serverCollection != value)
                {
                    _serverCollection = value;
                    OnPropertyChanged();
                    ServersChanged?.Invoke(this, new EventArgs());
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
