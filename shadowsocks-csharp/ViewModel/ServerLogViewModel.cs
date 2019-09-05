using Shadowsocks.Controller;
using Shadowsocks.Model;
using System.Collections.ObjectModel;

namespace Shadowsocks.ViewModel
{
    public class ServerLogViewModel : ViewModelBase
    {
        public ServerLogViewModel()
        {
            _serversCollection = new ObservableCollection<Server>();
        }

        private ObservableCollection<Server> _serversCollection;
        public ObservableCollection<Server> ServersCollection
        {
            get => _serversCollection;
            set
            {
                if (_serversCollection != value)
                {
                    _serversCollection = value;
                    OnPropertyChanged();
                }
            }
        }

        private Server _selectedServer;
        public Server SelectedServer
        {
            get => _selectedServer;
            private set
            {
                if (_selectedServer != value)
                {
                    _selectedServer = value;
                    OnPropertyChanged();
                }
            }
        }

        public void ReadConfig(ShadowsocksController controller)
        {
            var config = controller.GetCurrentConfiguration();
            ServersCollection.Clear();
            var index = 1;
            foreach (var server in config.configs)
            {
                server.Index = index++;
                if (config.index == server.Index - 1)
                {
                    server.IsSelected = true;
                    SelectedServer = server;
                }
                else
                {
                    server.IsSelected = false;
                }
                ServersCollection.Add(server);
            }
        }
    }
}