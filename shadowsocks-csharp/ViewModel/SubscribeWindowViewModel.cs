using System;
using System.Collections.ObjectModel;
using Shadowsocks.Model;

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
            foreach (var serverSubscribe in config.serverSubscribes)
            {
                SubscribeCollection.Add(serverSubscribe);
            }
            SelectedServer = SubscribeCollection.Count > 0 ? SubscribeCollection[0] : null;
            SubscribeCollection.CollectionChanged -= SubscribeCollection_CollectionChanged;
            SubscribeCollection.CollectionChanged += SubscribeCollection_CollectionChanged;
        }

        private void SubscribeCollection_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SubscribesChanged?.Invoke(this, new EventArgs());
        }

        public event EventHandler SubscribesChanged;

        private ObservableCollection<ServerSubscribe> _subscribeCollection;
        public ObservableCollection<ServerSubscribe> SubscribeCollection
        {
            get => _subscribeCollection;
            set
            {
                if (_subscribeCollection != value)
                {
                    _subscribeCollection = value;
                    OnPropertyChanged();
                    SubscribesChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        private ServerSubscribe _selectedServer;
        public ServerSubscribe SelectedServer
        {
            get => _selectedServer;
            set
            {
                if (_selectedServer != value)
                {
                    _selectedServer = value;
                    OnPropertyChanged();
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
            SubscribesChanged?.Invoke(this, new EventArgs());
        }
    }
}
