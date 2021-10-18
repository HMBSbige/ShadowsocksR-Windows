using Shadowsocks.Model;
using System;
using System.Collections.ObjectModel;

namespace Shadowsocks.ViewModel
{
    public class SubscribeWindowViewModel : ViewModelBase
    {
        public SubscribeWindowViewModel()
        {
            SubscribeCollection = new ObservableCollection<ServerSubscribe>();
        }

        public void ReadConfig(Configuration config)
        {
            SubscribeCollection.Clear();
            foreach (var serverSubscribe in config.ServerSubscribes)
            {
                SubscribeCollection.Add(serverSubscribe);
            }
            SelectedServer = SubscribeCollection.Count > 0 ? SubscribeCollection[0] : null;
            SubscribeCollection.CollectionChanged -= SubscribeCollection_CollectionChanged;
            SubscribeCollection.CollectionChanged += SubscribeCollection_CollectionChanged;
        }

        private void SubscribeCollection_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SubscribesChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler SubscribesChanged;

        private ObservableCollection<ServerSubscribe> _subscribeCollection;
        public ObservableCollection<ServerSubscribe> SubscribeCollection
        {
            get => _subscribeCollection;
            set
            {
                if (SetField(ref _subscribeCollection, value))
                {
                    SubscribesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private ServerSubscribe _selectedServer;
        public ServerSubscribe SelectedServer
        {
            get => _selectedServer;
            set
            {
                if (SetField(ref _selectedServer, value))
                {
                    if (_selectedServer != null)
                    {
                        _selectedServer.SubscribeChanged -= _selectedServer_SubscribeChanged;
                        _selectedServer.SubscribeChanged += _selectedServer_SubscribeChanged;
                    }
                }
            }
        }

        private void _selectedServer_SubscribeChanged(object sender, EventArgs e)
        {
            SubscribesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
