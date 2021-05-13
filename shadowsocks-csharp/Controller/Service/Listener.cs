using Shadowsocks.Enums;
using Shadowsocks.Model;
using Shadowsocks.Util;
using Shadowsocks.Util.NetUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Shadowsocks.Controller.Service
{
    public class Listener
    {
        public interface IService
        {
            bool Handle(byte[] firstPacket, int length, Socket socket);

            void Stop();
        }

        public abstract class Service : IService
        {
            public abstract bool Handle(byte[] firstPacket, int length, Socket socket);

            public virtual void Stop() { }
        }

        private Configuration _config;
        private bool _shareOverLan;
        private string _authUser;
        private Socket _socket;
        private Socket _socketV6;
        private bool _stop;
        private readonly List<IService> _services;

        public Listener(List<IService> services)
        {
            _services = services;
            _stop = false;
        }

        private static bool CheckIfPortInUse(int port)
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            return ipProperties.GetActiveTcpListeners().Any(endPoint => endPoint.Port == port);
        }

        public void Start(Configuration config, int port)
        {
            _config = config;
            _shareOverLan = config.ShareOverLan;
            _authUser = config.AuthUser;

            var localPort = port == 0 ? _config.LocalPort : port;
            if (CheckIfPortInUse(localPort))
            {
                throw new Exception(string.Format(I18NUtil.GetAppStringValue(@"PortInUse"), localPort));
            }

            try
            {
                //TODO:UDP socket
                // Create a TCP/IP socket.
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                var localEndPoint = new IPEndPoint(_shareOverLan ? IPAddress.Any : IPAddress.Loopback, localPort);
                // Bind the socket to the local endpoint and listen for incoming connections.
                _socket.Bind(localEndPoint);
                _socket.Listen(1024);

                // IPv6
                if (Global.OSSupportsLocalIPv6)
                {
                    try
                    {
                        _socketV6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                        _socketV6.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    }
                    catch
                    {
                        _socketV6 = null;
                    }
                    var localEndPointV6 = new IPEndPoint(_shareOverLan ? IPAddress.IPv6Any : IPAddress.IPv6Loopback, localPort);
                    if (_socketV6 != null)
                    {
                        _socketV6.Bind(localEndPointV6);
                        _socketV6.Listen(1024);
                    }
                }
                else
                {
                    _socketV6 = null;
                }

                // Start an asynchronous socket to listen for connections.
                Console.WriteLine($@"ShadowsocksR started on port {localPort}");
                _socket.BeginAccept(AcceptCallback, _socket);
                _socketV6?.BeginAccept(AcceptCallback, _socketV6);
            }
            catch (SocketException e)
            {
                Logging.LogUsefulException(e);
                if (_socket != null)
                {
                    _socket.Close();
                    _socket = null;
                }
                if (_socketV6 != null)
                {
                    _socketV6.Close();
                    _socketV6 = null;
                }
                throw;
            }
        }

        public void Stop()
        {
            _stop = true;

            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }
            if (_socketV6 != null)
            {
                _socketV6.Close();
                _socketV6 = null;
            }

            _services.ForEach(s => s.Stop());
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            if (_stop)
            {
                return;
            }
            var listener = (Socket)ar.AsyncState;
            try
            {
                var conn = listener.EndAccept(ar);

                var localPort = ((IPEndPoint)conn.LocalEndPoint).Port;

                if ((_authUser ?? string.Empty).Length == 0 && !IPSubnet.IsLan(conn)
                    && !(_config.PortMapCache.ContainsKey(localPort)
                    || _config.PortMapCache[localPort].type == PortMapType.Forward))
                {
                    conn.Shutdown(SocketShutdown.Both);
                    conn.Close();
                }
                else
                {
                    var buf = new byte[4096];
                    object[] state = {
                        conn,
                        buf
                    };

                    if (_config.PortMapCache.TryGetValue(localPort, out var portMap)
                    && portMap.type == PortMapType.Forward)
                    {
                        if (_services.Any(service => service.Handle(buf, 0, conn)))
                        {
                            return;
                        }

                        // no service found for this
                        // shouldn't happen
                        conn.Shutdown(SocketShutdown.Both);
                        conn.Close();
                    }
                    else
                    {
                        conn.BeginReceive(buf, 0, buf.Length, 0, ReceiveCallback, state);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                try
                {
                    listener.BeginAccept(AcceptCallback, listener);
                }
                catch (ObjectDisposedException)
                {
                    // do nothing
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                }
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            var state = (object[])ar.AsyncState;

            var conn = (Socket)state[0];
            var buf = (byte[])state[1];
            try
            {
                var bytesRead = conn.EndReceive(ar);
                if (bytesRead > 0 && _services.Any(service => service.Handle(buf, bytesRead, conn)))
                {
                    return;
                }

                // no service found for this
                // shouldn't happen
                conn.Shutdown(SocketShutdown.Both);
                conn.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                conn.Shutdown(SocketShutdown.Both);
                conn.Close();
            }
        }
    }
}
