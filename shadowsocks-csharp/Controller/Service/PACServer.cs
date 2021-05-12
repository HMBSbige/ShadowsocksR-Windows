using Shadowsocks.Enums;
using Shadowsocks.Model;
using Shadowsocks.Util;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Shadowsocks.Controller.Service
{
    public class PACServer : Listener.Service
    {
        private const string ResourceName = @"pac";

        public static string gfwlist_FILE = @"gfwlist.txt";

        public static string WHITELIST_FILE = @"whitelist.txt";

        public static string USER_WHITELIST_TEMPLATE_FILE = @"user_whitelist_temp.txt";

        public string PacUrl { get; private set; } = string.Empty;

        private Configuration _config;
        private readonly PACDaemon _pacDaemon;

        public PACServer(PACDaemon pacDaemon)
        {
            _pacDaemon = pacDaemon;
        }

        public void UpdatePacUrl(Configuration config)
        {
            _config = config;
            //Lots of software do not support ipv6 pac url yet
            PacUrl = $@"http://{IPAddress.Loopback}:{config.LocalPort}/{ResourceName}?t={DateTime.Now:yyyyMMddHHmmssffff}";
        }

        public override bool Handle(byte[] firstPacket, int length, Socket socket)
        {
            if (socket.ProtocolType != ProtocolType.Tcp)
            {
                return false;
            }
            try
            {
                var request = Encoding.UTF8.GetString(firstPacket, 0, length);
                var lines = request.GetLines();
                bool hostMatch = false, pathMatch = false;
                var socksType = 0;
                string proxy = null;

                foreach (var line in lines)
                {
                    var kv = line.Split(new[] { ':' }, 2);
                    switch (kv.Length)
                    {
                        case 2:
                        {
                            if (kv[0] == @"Host")
                            {
                                if (kv[1].Trim() == ((IPEndPoint)socket.LocalEndPoint).ToString())
                                {
                                    hostMatch = true;
                                }
                            }
                            break;
                        }
                        case 1:
                        {
                            if (line.IndexOf($" /{ResourceName}?", StringComparison.Ordinal) > 0 && line.IndexOf(@"GET", StringComparison.Ordinal) == 0)
                            {
                                var url = line.Substring(line.IndexOf(" ", StringComparison.Ordinal) + 1);
                                url = url.Substring(0, url.IndexOf(" ", StringComparison.Ordinal));
                                pathMatch = true;
                                var portPos = url.IndexOf("port=", StringComparison.Ordinal);
                                if (portPos > 0)
                                {
                                    var port = url.Substring(portPos + 5);
                                    if (port.IndexOf("&", StringComparison.Ordinal) >= 0)
                                    {
                                        port = port.Substring(0, port.IndexOf("&", StringComparison.Ordinal));
                                    }

                                    var ipPos = url.IndexOf("ip=", StringComparison.Ordinal);
                                    if (ipPos > 0)
                                    {
                                        proxy = url.Substring(ipPos + 3);
                                        if (proxy.IndexOf("&", StringComparison.Ordinal) >= 0)
                                        {
                                            proxy = proxy.Substring(0, proxy.IndexOf("&", StringComparison.Ordinal));
                                        }

                                        proxy += $@":{port};";
                                    }
                                    else
                                    {
                                        proxy = $@"127.0.0.1:{port};";
                                    }
                                }

                                if (url.IndexOf("type=socks4", StringComparison.Ordinal) > 0 ||
                                    url.IndexOf("type=s4", StringComparison.Ordinal) > 0)
                                {
                                    socksType = 4;
                                }

                                if (url.IndexOf("type=socks5", StringComparison.Ordinal) > 0 ||
                                    url.IndexOf("type=s5", StringComparison.Ordinal) > 0)
                                {
                                    socksType = 5;
                                }
                            }
                            break;
                        }
                    }
                }
                if (hostMatch && pathMatch)
                {
                    SendResponse(socket, socksType, proxy);
                    return true;
                }
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private void SendResponse(Socket socket, int socksType, string setProxy)
        {
            try
            {
                var pac = _pacDaemon.GetPACContent();

                var localEndPoint = (IPEndPoint)socket.LocalEndPoint;

                var proxy =
                    setProxy == null ? GetPACAddress(localEndPoint, socksType) :
                    socksType == 5 ? $@"SOCKS5 {setProxy}" :
                    socksType == 4 ? $@"SOCKS {setProxy}" :
                    $@"PROXY {setProxy}";

                if (_config.PacDirectGoProxy && _config.ProxyEnable)
                {
                    if (_config.ProxyType == ProxyType.Socks5)
                    {
                        pac = pac.Replace(@"__DIRECT__", $@"SOCKS5 {_config.ProxyHost}:{_config.ProxyPort};DIRECT;");
                    }
                    else if (_config.ProxyType == ProxyType.Http)
                    {
                        pac = pac.Replace(@"__DIRECT__", $@"PROXY {_config.ProxyHost}:{_config.ProxyPort};DIRECT;");
                    }
                }
                else
                {
                    pac = pac.Replace(@"__DIRECT__", @"DIRECT;");
                }

                pac = pac.Replace(@"__PROXY__", $@"{proxy}DIRECT;");

                var text =
$@"HTTP/1.1 200 OK
Server: {HttpRequest.UpdateChecker.Name}/{HttpRequest.UpdateChecker.Version}
Content-Type: application/x-ns-proxy-autoconfig
Content-Length: {Encoding.UTF8.GetBytes(pac).Length}
Connection: Close

{pac}";
                var response = Encoding.UTF8.GetBytes(text);
                socket.BeginSend(response, 0, response.Length, 0, SendCallback, socket);
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
        }

        private static void SendCallback(IAsyncResult ar)
        {
            var conn = (Socket)ar.AsyncState;
            try
            {
                conn.Shutdown(SocketShutdown.Both);
                conn.Close();
            }
            catch
            {
                // ignored
            }
        }

        private string GetPACAddress(IPEndPoint localEndPoint, int socksType)
        {
            var localhost = localEndPoint.AddressFamily == AddressFamily.InterNetworkV6
                    ? $@"[{localEndPoint.Address}]"
                    : $@"{localEndPoint.Address}";
            return socksType switch
            {
                5 => $@"SOCKS5 {localhost}:{_config.LocalPort};",
                4 => $@"SOCKS {localhost}:{_config.LocalPort};",
                _ => $@"PROXY {localhost}:{_config.LocalPort};"
            };
        }
    }
}
