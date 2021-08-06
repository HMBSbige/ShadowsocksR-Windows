using HttpProxy;
using Microsoft.VisualStudio.Threading;
using Shadowsocks.Model;
using Socks5.Models;
using System;
using System.Net;
using System.Net.Sockets;

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
            RunningPort = GetFreePort(ip);
            var ipe = new IPEndPoint(ip, RunningPort);
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

        private static int GetFreePort(IPAddress bindIp)
        {
            const int defaultPort = 60000;
            try
            {
                var l = new TcpListener(bindIp, 0);
                l.Start();
                var port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                return port;
            }
            catch (Exception ex)
            {
                Logging.LogUsefulException(ex);
                return defaultPort;
            }
        }
    }
}
