using Shadowsocks.Util;
using Shadowsocks.ViewModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Shadowsocks.Model
{
    public class ServerTreeViewModel : ViewModelBase
    {
        public ServerTreeViewModel()
        {
            _nodes = new ObservableCollection<ServerTreeViewModel>();
            _name = string.Empty;
            _server = null;
            _type = ServerTreeViewType.Subtag;
        }

        private string _name;
        public string Name
        {
            get
            {
                switch (Type)
                {
                    case ServerTreeViewType.Subtag:
                        if (string.IsNullOrEmpty(_name))
                        {
                            return I18NUtil.GetAppStringValue(@"EmptySubtag");
                        }
                        break;
                    case ServerTreeViewType.Group:
                        if (string.IsNullOrEmpty(_name))
                        {
                            return I18NUtil.GetAppStringValue(@"EmptyGroup");
                        }
                        break;
                    case ServerTreeViewType.Server:
                        if (Server != null)
                        {
                            return Server.FriendlyName;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(Type));
                }
                return _name;
            }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<ServerTreeViewModel> _nodes;
        public ObservableCollection<ServerTreeViewModel> Nodes
        {
            get => _nodes;
            set
            {
                if (_nodes != value)
                {
                    _nodes = value;
                    OnPropertyChanged();
                }
            }
        }

        private ServerTreeViewType _type;
        public ServerTreeViewType Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged();
                }
            }
        }

        private Server _server;
        public Server Server
        {
            get => _server;
            set
            {
                if (_server != value)
                {
                    _server = value;
                    OnPropertyChanged();
                    _server.ServerChanged += Server_ServerChanged;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private void Server_ServerChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(Name));
        }

        #region Static Method

        public static void Remove(ObservableCollection<ServerTreeViewModel> root, ServerTreeViewModel st)
        {
            if (root.Remove(st))
            {
                return;
            }

            foreach (var serverTreeViewModel in root)
            {
                Remove(serverTreeViewModel.Nodes, st);
            }
        }

        public static ServerTreeViewModel FindNode(Collection<ServerTreeViewModel> root, string serverId)
        {
            var res = root.FirstOrDefault(serverTreeViewModel => serverTreeViewModel.Server?.Id == serverId);
            if (res != null)
            {
                return res;
            }

            foreach (var serverTreeViewModel in root)
            {
                res = FindNode(serverTreeViewModel.Nodes, serverId);
                if (res != null)
                {
                    return res;
                }
            }
            return null;
        }

        #endregion
    }
}
