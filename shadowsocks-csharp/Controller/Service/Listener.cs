using Shadowsocks.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Timers;

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
        private bool _shareOverLAN;
        private string _authUser;
        private Socket _socket;
        private Socket _socketV6;
        private bool _stop;
        private readonly IList<Service> _services;
        protected Timer timer;
        protected readonly object TimerLock = new object();

        public Listener(IList<Service> services)
        {
            _services = services;
            _stop = false;
        }

        private static bool CheckIfPortInUse(int port)
        {
            try
            {
                var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                var ipEndPoints = ipProperties.GetActiveTcpListeners();

                if (ipEndPoints.Any(endPoint => endPoint.Port == port))
                {
                    return true;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        public void Start(Configuration config, int port)
        {
            _config = config;
            _shareOverLAN = config.shareOverLan;
            _authUser = config.authUser;
            _stop = false;

            var localPort = port == 0 ? _config.localPort : port;
            if (CheckIfPortInUse(localPort))
                throw new Exception(string.Format(I18N.GetString("Port {0} already in use"), _config.localPort));

            try
            {
                // Create a TCP/IP socket.
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                try
                {
                    _socketV6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                    _socketV6.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                }
                catch
                {
                    _socketV6 = null;
                }

                var localEndPoint = new IPEndPoint(IPAddress.Any, localPort);
                var localEndPointV6 = new IPEndPoint(IPAddress.IPv6Any, localPort);

                // Bind the socket to the local endpoint and listen for incoming connections.
                _socket.Bind(localEndPoint);
                _socket.Listen(1024);
                if (_socketV6 != null)
                {
                    _socketV6.Bind(localEndPointV6);
                    _socketV6.Listen(1024);
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
            ResetTimeout(0, null);
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
        }

        private void ResetTimeout(double time, Socket socket)
        {
            if (time <= 0 && timer == null)
                return;

            lock (TimerLock)
            {
                void OnTimerOnElapsed(object sender, ElapsedEventArgs e) => Timer_Elapsed(socket);
                if (time <= 0)
                {
                    if (timer != null)
                    {
                        timer.Enabled = false;
                        timer.Elapsed -= OnTimerOnElapsed;
                        timer.Dispose();
                        timer = null;
                    }
                }
                else
                {
                    if (timer == null)
                    {
                        timer = new Timer(time * 1000.0);
                        timer.Elapsed += OnTimerOnElapsed;
                        timer.Start();
                    }
                    else
                    {
                        timer.Interval = time * 1000.0;
                        timer.Stop();
                        timer.Start();
                    }
                }
            }
        }

        private void Timer_Elapsed(Socket socket)
        {
            if (timer == null)
            {
                return;
            }
            var listener = socket;
            try
            {
                listener.BeginAccept(AcceptCallback, listener);
                ResetTimeout(0, listener);
            }
            catch (ObjectDisposedException)
            {
                // do nothing
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                ResetTimeout(5, listener);
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            if (_stop) return;

            var listener = (Socket)ar.AsyncState;
            try
            {
                var conn = listener.EndAccept(ar);

                if (!_shareOverLAN && !Util.Utils.isLocal(conn))
                {
                    conn.Shutdown(SocketShutdown.Both);
                    conn.Close();
                }

                var localPort = ((IPEndPoint)conn.LocalEndPoint).Port;

                if ((_authUser ?? string.Empty).Length == 0 && !Util.Utils.isLAN(conn)
                    && !(_config.GetPortMapCache().ContainsKey(localPort)
                    || _config.GetPortMapCache()[localPort].type == PortMapType.Forward))
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

                    if (!_config.GetPortMapCache().ContainsKey(localPort) || _config.GetPortMapCache()[localPort].type != PortMapType.Forward)
                    {
                        conn.BeginReceive(buf, 0, buf.Length, 0, ReceiveCallback, state);
                    }
                    else
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
                    ResetTimeout(5, listener);
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
                if (_services.Any(service => service.Handle(buf, bytesRead, conn)))
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
