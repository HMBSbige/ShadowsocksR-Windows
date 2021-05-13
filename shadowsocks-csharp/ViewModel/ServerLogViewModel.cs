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
            set => SetField(ref _serversCollection, value);
        }

        private Server _selectedServer;
        public Server SelectedServer
        {
            get => _selectedServer;
            private set => SetField(ref _selectedServer, value);
        }

        public void ReadConfig()
        {
            var config = Global.GuiConfig;
            ServersCollection.Clear();
            var index = 1;
            foreach (var server in config.Configs)
            {
                server.Index = index++;
                if (config.Index == server.Index - 1)
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
