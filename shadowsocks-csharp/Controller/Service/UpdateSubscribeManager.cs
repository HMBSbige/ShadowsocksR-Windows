using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Model;
using System.Collections.Generic;
using System.Linq;

namespace Shadowsocks.Controller.Service
{
    public class UpdateSubscribeManager
    {
        private Configuration _config;
        private Queue<ServerSubscribe> _serverSubscribes;
        private UpdateNode _updater;
        private bool _notify;

        public void CreateTask(Configuration config, UpdateNode updater, bool updateManually, ServerSubscribe serverSubscribe = null)
        {
            if (_config == null)
            {
                _config = config;
                _updater = updater;
                _notify = updateManually;
                _serverSubscribes = new Queue<ServerSubscribe>();
                if (serverSubscribe != null)
                {
                    _serverSubscribes.Enqueue(serverSubscribe);
                }
                else
                {
                    if (updateManually)
                    {
                        config.serverSubscribes.ForEach(sub => _serverSubscribes.Enqueue(sub));
                    }
                    else
                    {
                        foreach (var server in config.serverSubscribes.Where(server => server.AutoCheckUpdate))
                        {
                            _serverSubscribes.Enqueue(server);
                        }
                    }
                }
                Next();
            }
        }

        public void Next()
        {
            if (_serverSubscribes.Count == 0)
            {
                _config = null;
                return;
            }

            CurrentServerSubscribe = _serverSubscribes.Dequeue();
            _updater.CheckUpdate(_config, CurrentServerSubscribe, _notify);
        }

        public ServerSubscribe CurrentServerSubscribe { get; private set; }
    }
}