using System.Collections.Generic;
using Shadowsocks.Model;

namespace Shadowsocks.Controller
{
    public class UpdateSubscribeManager
    {
        private Configuration _config;
        private List<ServerSubscribe> _serverSubscribes;
        private UpdateFreeNode _updater;
        private bool _useProxy;
        private bool _notify;

        public void CreateTask(Configuration config, UpdateFreeNode updater, int index, bool useProxy, bool notify)
        {
            if (_config == null)
            {
                _config = config;
                _updater = updater;
                _useProxy = useProxy;
                _notify = notify;
                if (index < 0)
                {
                    _serverSubscribes = new List<ServerSubscribe>();
                    foreach (var server in config.serverSubscribes)
                    {
                        _serverSubscribes.Add(server);
                    }
                }
                else if (index < _config.serverSubscribes.Count)
                {
                    _serverSubscribes = new List<ServerSubscribe> { config.serverSubscribes[index] };
                }
                Next();
            }
        }

        public bool Next()
        {
            if (_serverSubscribes.Count == 0)
            {
                _config = null;
                return false;
            }

            Url = _serverSubscribes[0].Url;
            _updater.CheckUpdate(_config, _serverSubscribes[0], _useProxy, _notify);
            _serverSubscribes.RemoveAt(0);
            return true;
        }

        public string Url { get; private set; }
    }
}