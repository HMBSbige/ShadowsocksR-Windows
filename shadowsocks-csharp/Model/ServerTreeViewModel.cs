using Shadowsocks.Enums;
using Shadowsocks.Util;
using Shadowsocks.ViewModel;
using System;
using System.Collections.Generic;
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
            set => SetField(ref _name, value);
        }

        private ObservableCollection<ServerTreeViewModel> _nodes;
        public ObservableCollection<ServerTreeViewModel> Nodes
        {
            get => _nodes;
            set => SetField(ref _nodes, value);
        }

        private ServerTreeViewType _type;
        public ServerTreeViewType Type
        {
            get => _type;
            set => SetField(ref _type, value);
        }

        private Server _server;
        public Server Server
        {
            get => _server;
            set
            {
                if (SetField(ref _server, value))
                {
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

        public static ServerTreeViewModel FindParentNode(Collection<ServerTreeViewModel> root, ServerTreeViewModel st)
        {
            var res = root.Where(serverTreeViewModel => serverTreeViewModel.Nodes != null)
                    .FirstOrDefault(serverTreeViewModel => serverTreeViewModel.Nodes.Contains(st));
            if (res != null)
            {
                return res;
            }

            foreach (var serverTreeViewModel in root)
            {
                res = FindParentNode(serverTreeViewModel.Nodes, st);
                if (res != null)
                {
                    return res;
                }
            }

            return null;
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
