using Shadowsocks.Model;
using System.Collections.Generic;
using System.Linq;

namespace Shadowsocks.Controller
{
    public class UpdateSubscribeManager
    {
        private Configuration _config;
        private List<ServerSubscribe> _serverSubscribes;
        private UpdateFreeNode _updater;
        private bool _useProxy;
        private bool _notify;

        public void CreateTask(Configuration config, UpdateFreeNode updater, bool useProxy, bool updateManually, ServerSubscribe serverSubscribe = null)
        {
            if (_config == null)
            {
                _config = config;
                _updater = updater;
                _useProxy = useProxy;
                _notify = updateManually;
                _serverSubscribes = new List<ServerSubscribe>();
                if (serverSubscribe != null)
                {
                    _serverSubscribes.Add(serverSubscribe);
                }
                else
                {
                    if (updateManually)
                    {
                        _serverSubscribes.AddRange(config.serverSubscribes);
                    }
                    else
                    {
                        foreach (var server in config.serverSubscribes.Where(server => server.AutoCheckUpdate))
                        {
                            _serverSubscribes.Add(server);
                        }
                    }
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

            CurrentServerSubscribe = _serverSubscribes[0];
            _updater.CheckUpdate(_config, _serverSubscribes[0], _useProxy, _notify);
            _serverSubscribes.RemoveAt(0);
            return true;
        }

        public ServerSubscribe CurrentServerSubscribe { get; private set; }
    }
}