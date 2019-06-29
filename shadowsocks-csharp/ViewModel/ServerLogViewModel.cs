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

        public void ReadConfig(ShadowsocksController controller)
        {
            var config = controller.GetCurrentConfiguration();
            ServersCollection = config.configs;
            var index = 1;
            foreach (var server in ServersCollection)
            {
                server.Index = index++;
                server.IsSelected = config.index == server.Index - 1;
            }
        }
    }
}