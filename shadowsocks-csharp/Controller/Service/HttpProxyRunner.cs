using HttpProxy;
using Microsoft.VisualStudio.Threading;
using Shadowsocks.Model;
using Socks5.Models;
using System.Net;

#nullable enable

namespace Shadowsocks.Controller.Service
{
    public class HttpProxyRunner
    {
        private HttpSocks5Service? _service;

        public int RunningPort { get; private set; }

        public void Start(Configuration configuration)
        {
            if (_service is not null)
            {
                return;
            }

            var ip = configuration.ShareOverLan ? Global.IpAny : Global.IpLocal;
            var ipe = new IPEndPoint(ip, 0);
            var option = new Socks5CreateOption
            {
                Address = Global.IpLocal,
                Port = (ushort)configuration.LocalPort,
                UsernamePassword = new UsernamePassword
                {
                    UserName = configuration.AuthUser,
                    Password = configuration.AuthPass
                }
            };
            _service = new HttpSocks5Service(ipe, new HttpToSocks5(), option);
            _service.StartAsync().Forget();
            RunningPort = ((IPEndPoint)_service.TcpListener.LocalEndpoint).Port;
        }

        public void Stop()
        {
            if (_service is null)
            {
                return;
            }

            _service.Stop();
            _service = null;
        }
    }
}
