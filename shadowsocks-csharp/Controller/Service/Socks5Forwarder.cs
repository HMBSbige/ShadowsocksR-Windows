using Shadowsocks.Enums;
using Shadowsocks.Model;
using Shadowsocks.Proxy;
using Shadowsocks.Util.NetUtils;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Shadowsocks.Controller.Service
{
    class Socks5Forwarder : Listener.Service
    {
        private Configuration _config;
        private IPRangeSet _IPRange;
        const int CONNECT_DIRECT = 1;
        const int CONNECT_LOCALPROXY = 2;
        const int CONNECT_REMOTEPROXY = 0;

        public Socks5Forwarder(Configuration config, IPRangeSet IPRange)
        {
            _config = config;
            _IPRange = IPRange;
        }

        public override bool Handle(byte[] firstPacket, int length, Socket socket)
        {
            return Handle(firstPacket, length, socket, null);
        }

        public bool Handle(byte[] firstPacket, int length, Socket socket, string local_sendback_protocol)
        {
            var handle = IsHandle(firstPacket, length);
            if (handle > 0)
            {
                if (_config.ProxyEnable)
                {
                    new Handler().Start(_config, firstPacket, socket, local_sendback_protocol, handle == 2);
                }
                else
                {
                    new Handler().Start(_config, firstPacket, socket, local_sendback_protocol, false);
                }
                return true;
            }
            return false;
        }

        private int IsHandle(byte[] firstPacket, int length)
        {
            if (length >= 7 && _config.ProxyRuleMode != ProxyRuleMode.Disable)
            {
                IPAddress ipAddress = null;
                if (firstPacket[0] == 1)
                {
                    var addr = new byte[4];
                    Array.Copy(firstPacket, 1, addr, 0, addr.Length);
                    ipAddress = new IPAddress(addr);
                }
                else if (firstPacket[0] == 3)
                {
                    int len = firstPacket[1];
                    var addr = new byte[len];
                    if (length >= len + 2)
                    {
                        Array.Copy(firstPacket, 2, addr, 0, addr.Length);
                        var host = Encoding.UTF8.GetString(firstPacket, 2, len);
                        if (IPAddress.TryParse(host, out ipAddress))
                        {
                            //pass
                        }
                        else
                        {
                            if ((_config.ProxyRuleMode == ProxyRuleMode.BypassLanAndChina || _config.ProxyRuleMode == ProxyRuleMode.BypassLanAndNotChina) && _IPRange != null || _config.ProxyRuleMode == ProxyRuleMode.UserCustom)
                            {
                                if (!IPAddress.TryParse(host, out ipAddress))
                                {
                                    if (_config.ProxyRuleMode == ProxyRuleMode.UserCustom)
                                    {
                                        if (HostMap.GetHost(host, out var host_addr))
                                        {
                                            if (!string.IsNullOrEmpty(host_addr))
                                            {
                                                var lower_host_addr = host_addr.ToLower();
                                                if (lower_host_addr.StartsWith("reject")
                                                    || lower_host_addr.StartsWith("direct")
                                                    )
                                                {
                                                    return CONNECT_DIRECT;
                                                }

                                                if (lower_host_addr.StartsWith("localproxy"))
                                                {
                                                    return CONNECT_LOCALPROXY;
                                                }

                                                if (lower_host_addr.StartsWith("remoteproxy"))
                                                {
                                                    return CONNECT_REMOTEPROXY;
                                                }

                                                if (lower_host_addr.IndexOf('.') >= 0 || lower_host_addr.IndexOf(':') >= 0)
                                                {
                                                    if (!IPAddress.TryParse(lower_host_addr, out ipAddress))
                                                    {
                                                        //
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (ipAddress == null)
                                    {
                                        ipAddress = DnsUtil.DnsBuffer.Get(host);
                                    }
                                }
                                if (ipAddress == null)
                                {
                                    ipAddress = DnsUtil.QueryDns(host);
                                    if (ipAddress != null)
                                    {
                                        DnsUtil.DnsBuffer.Set(host, new IPAddress(ipAddress.GetAddressBytes()));
                                        if (host.IndexOf('.') >= 0 && IPSubnet.IsLan(ipAddress))
                                        {
                                            // assume that it is pollution if return LAN address
                                            return CONNECT_REMOTEPROXY;
                                        }
                                    }
                                    else
                                    {
                                        Logging.Log(LogLevel.Debug, "DNS query fail: " + host);
                                    }
                                }
                            }
                        }
                    }
                }
                else if (firstPacket[0] == 4)
                {
                    var addr = new byte[16];
                    Array.Copy(firstPacket, 1, addr, 0, addr.Length);
                    ipAddress = new IPAddress(addr);
                }
                if (ipAddress != null)
                {
                    if (_config.ProxyRuleMode == ProxyRuleMode.UserCustom)
                    {
                        if (HostMap.GetIP(ipAddress, out var host_addr))
                        {
                            var lower_host_addr = host_addr.ToLower();
                            if (lower_host_addr.StartsWith("reject")
                                || lower_host_addr.StartsWith("direct")
                                )
                            {
                                return CONNECT_DIRECT;
                            }

                            if (lower_host_addr.StartsWith("localproxy"))
                            {
                                return CONNECT_LOCALPROXY;
                            }

                            if (lower_host_addr.StartsWith("remoteproxy"))
                            {
                                return CONNECT_REMOTEPROXY;
                            }
                        }
                    }
                    else
                    {
                        if (IPSubnet.IsLan(ipAddress))
                        {
                            return CONNECT_DIRECT;
                        }
                        if (_config.ProxyRuleMode is ProxyRuleMode.BypassLanAndChina or ProxyRuleMode.BypassLanAndNotChina && _IPRange is not null)
                        {
                            if (_IPRange.IsInIPRange(ipAddress))
                            {
                                return CONNECT_LOCALPROXY;
                            }

                            DnsUtil.DnsBuffer.Sweep();
                        }
                    }
                }
            }
            return CONNECT_REMOTEPROXY;
        }

        private class Handler : IHandler
        {
            private Configuration _config;

            private byte[] _firstPacket;
            private ProxySocketTunLocal _local;
            private ProxySocketTun _remote;

            private bool _closed;
            private bool _local_proxy;
            private string _remote_host;
            private int _remote_port;

            public const int RecvSize = 1460 * 8;
            // remote receive buffer
            private byte[] remoteRecvBuffer = new byte[RecvSize];
            // connection receive buffer
            private byte[] connetionRecvBuffer = new byte[RecvSize];
            private int _totalRecvSize;

            protected int TTL = 600;
            protected System.Timers.Timer timer;
            protected object timerLock = new();
            protected DateTime lastTimerSetTime;

            public void Start(Configuration config, byte[] firstPacket, Socket socket, string local_sendback_protocol, bool proxy)
            {
                _firstPacket = firstPacket;
                _local = new ProxySocketTunLocal(socket)
                {
                    local_sendback_protocol = local_sendback_protocol
                };
                _config = config;
                _local_proxy = proxy;
                Connect();
            }

            private void Connect()
            {
                try
                {
                    IPAddress ipAddress = null;
                    var _targetPort = 0;
                    {
                        if (_firstPacket[0] == 1)
                        {
                            var addr = new byte[4];
                            Array.Copy(_firstPacket, 1, addr, 0, addr.Length);
                            ipAddress = new IPAddress(addr);
                            _targetPort = (_firstPacket[5] << 8) | _firstPacket[6];
                            _remote_host = ipAddress.ToString();
                            Logging.Info((_local_proxy ? "Local proxy" : "Direct") + " connect " + _remote_host + ":" + _targetPort);
                        }
                        else if (_firstPacket[0] == 4)
                        {
                            var addr = new byte[16];
                            Array.Copy(_firstPacket, 1, addr, 0, addr.Length);
                            ipAddress = new IPAddress(addr);
                            _targetPort = (_firstPacket[17] << 8) | _firstPacket[18];
                            _remote_host = ipAddress.ToString();
                            Logging.Info((_local_proxy ? "Local proxy" : "Direct") + " connect " + _remote_host + ":" + _targetPort);
                        }
                        else if (_firstPacket[0] == 3)
                        {
                            int len = _firstPacket[1];
                            var addr = new byte[len];
                            Array.Copy(_firstPacket, 2, addr, 0, addr.Length);
                            _remote_host = Encoding.UTF8.GetString(_firstPacket, 2, len);
                            _targetPort = (_firstPacket[len + 2] << 8) | _firstPacket[len + 3];
                            Logging.Info((_local_proxy ? "Local proxy" : "Direct") + " connect " + _remote_host + ":" + _targetPort);

                            //if (!_local_proxy)
                            {
                                if (!IPAddress.TryParse(_remote_host, out ipAddress))
                                {
                                    if (_config.ProxyRuleMode == ProxyRuleMode.UserCustom)
                                    {
                                        if (HostMap.GetHost(_remote_host, out var host_addr))
                                        {
                                            if (!string.IsNullOrEmpty(host_addr))
                                            {
                                                var lower_host_addr = host_addr.ToLower();
                                                if (lower_host_addr.StartsWith("reject"))
                                                {
                                                    Close();
                                                    return;
                                                }

                                                if (lower_host_addr.IndexOf('.') >= 0 || lower_host_addr.IndexOf(':') >= 0)
                                                {
                                                    if (!IPAddress.TryParse(lower_host_addr, out ipAddress))
                                                    {
                                                        //
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (ipAddress == null)
                                    {
                                        ipAddress = DnsUtil.DnsBuffer.Get(_remote_host);
                                    }
                                }
                                if (ipAddress == null)
                                {
                                    ipAddress = DnsUtil.QueryDns(_remote_host);
                                }
                                if (ipAddress != null)
                                {
                                    DnsUtil.DnsBuffer.Set(_remote_host, new IPAddress(ipAddress.GetAddressBytes()));
                                    DnsUtil.DnsBuffer.Sweep();
                                }
                                else
                                {
                                    if (!_local_proxy)
                                    {
                                        throw new SocketException((int)SocketError.HostNotFound);
                                    }
                                }
                            }
                        }
                        _remote_port = _targetPort;
                    }
                    if (ipAddress != null && _config.ProxyRuleMode == ProxyRuleMode.UserCustom)
                    {
                        if (HostMap.GetIP(ipAddress, out var host_addr))
                        {
                            var lower_host_addr = host_addr.ToLower();
                            if (lower_host_addr.StartsWith("reject")
                                )
                            {
                                Close();
                                return;
                            }
                        }
                    }
                    if (_local_proxy)
                    {
                        IPAddress.TryParse(_config.ProxyHost, out ipAddress);
                        _targetPort = _config.ProxyPort;
                    }
                    // ProxyAuth recv only socks5 head, so don't need to save anything else
                    var remoteEP = new IPEndPoint(ipAddress ?? throw new InvalidOperationException(), _targetPort);

                    _remote = new ProxySocketTun(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    _remote.GetSocket().NoDelay = true;

                    // Connect to the remote endpoint.
                    _remote.BeginConnect(remoteEP, ConnectCallback, null);
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
                }
            }

            private bool ConnectProxyServer(string strRemoteHost, int iRemotePort)
            {
                if (_config.ProxyType == ProxyType.Socks5)
                {
                    var ret = _remote.ConnectSocks5ProxyServer(strRemoteHost, iRemotePort, false, _config.ProxyAuthUser, _config.ProxyAuthPass);
                    return ret;
                }

                if (_config.ProxyType == ProxyType.Http)
                {
                    var ret = _remote.ConnectHttpProxyServer(strRemoteHost, iRemotePort, _config.ProxyAuthUser, _config.ProxyAuthPass, _config.ProxyUserAgent);
                    return ret;
                }

                return true;
            }

            private void ConnectCallback(IAsyncResult ar)
            {
                if (_closed)
                {
                    return;
                }
                try
                {
                    _remote.EndConnect(ar);
                    if (_local_proxy)
                    {
                        if (!ConnectProxyServer(_remote_host, _remote_port))
                        {
                            throw new SocketException((int)SocketError.ConnectionReset);
                        }
                    }
                    StartPipe();
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
                }
            }

            private void ResetTimeout(double time)
            {
                if (time <= 0 && timer == null)
                {
                    return;
                }

                if (time <= 0)
                {
                    if (timer != null)
                    {
                        lock (timerLock)
                        {
                            if (timer != null)
                            {
                                timer.Enabled = false;
                                timer.Elapsed -= timer_Elapsed;
                                timer.Dispose();
                                timer = null;
                            }
                        }
                    }
                }
                else
                {
                    if ((DateTime.Now - lastTimerSetTime).TotalMilliseconds > 500)
                    {
                        lock (timerLock)
                        {
                            if (timer == null)
                            {
                                timer = new System.Timers.Timer(time * 1000.0);
                                timer.Elapsed += timer_Elapsed;
                            }
                            else
                            {
                                timer.Interval = time * 1000.0;
                                timer.Stop();
                            }
                            timer.Start();
                            lastTimerSetTime = DateTime.Now;
                        }
                    }
                }
            }

            private void timer_Elapsed(object sender, ElapsedEventArgs e)
            {
                if (_closed)
                {
                    return;
                }
                Close();
            }

            private void StartPipe()
            {
                if (_closed)
                {
                    return;
                }
                try
                {
                    Server.ForwardServer.Connections.AddRef(this);
                    _remote.BeginReceive(remoteRecvBuffer, RecvSize, SocketFlags.None, PipeRemoteReceiveCallback, null);
                    _local.BeginReceive(connetionRecvBuffer, RecvSize, SocketFlags.None, PipeConnectionReceiveCallback, null);

                    _local.Send(connetionRecvBuffer, 0, SocketFlags.None);
                    ResetTimeout(TTL);
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
                }
            }

            private void PipeRemoteReceiveCallback(IAsyncResult ar)
            {
                if (_closed)
                {
                    return;
                }
                try
                {
                    var bytesRead = _remote.EndReceive(ar);

                    if (bytesRead > 0)
                    {
                        ResetTimeout(TTL);
                        //_local.BeginSend(remoteRecvBuffer, bytesRead, 0, new AsyncCallback(PipeConnectionSendCallback), null);
                        _local.Send(remoteRecvBuffer, bytesRead, SocketFlags.None);
                        _totalRecvSize += bytesRead;
                        if (_totalRecvSize <= 1024 * 1024 * 2)
                        {
                            _remote.BeginReceive(remoteRecvBuffer, RecvSize, SocketFlags.None, PipeRemoteReceiveCallback, null);
                        }
                        else
                        {
                            PipeRemoteReceiveLoop();
                        }
                    }
                    else
                    {
                        Close();
                    }
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
                }
            }

            private void PipeRemoteReceiveLoop()
            {
                var final_close = false;
                var recv_buffer = new byte[RecvSize];
                var beforeReceive = DateTime.Now;
                while (!_closed)
                {
                    try
                    {
                        var bytesRead = _remote.Receive(recv_buffer, RecvSize, SocketFlags.None);
                        var now = DateTime.Now;
                        if (_remote != null && _remote.IsClose)
                        {
                            final_close = true;
                            break;
                        }
                        if (_closed)
                        {
                            break;
                        }
                        ResetTimeout(TTL);

                        if (bytesRead > 0)
                        {
                            _local.Send(recv_buffer, bytesRead, SocketFlags.None);

                            if ((now - beforeReceive).TotalSeconds > 5)
                            {
                                _totalRecvSize = 0;
                                _remote.BeginReceive(remoteRecvBuffer, RecvSize, SocketFlags.None, PipeRemoteReceiveCallback, null);
                                return;
                            }

                            beforeReceive = now;
                        }
                        else
                        {
                            Close();
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.LogUsefulException(e);
                        final_close = true;
                        break;
                    }
                }
                if (final_close)
                {
                    Close();
                }
            }

            private void PipeConnectionReceiveCallback(IAsyncResult ar)
            {
                if (_closed)
                {
                    return;
                }
                try
                {
                    var bytesRead = _local.EndReceive(ar);

                    if (bytesRead > 0)
                    {
                        ResetTimeout(TTL);
                        //_remote.BeginSend(connetionRecvBuffer, bytesRead, 0, new AsyncCallback(PipeRemoteSendCallback), null);
                        _remote.Send(connetionRecvBuffer, bytesRead, SocketFlags.None);
                        _local.BeginReceive(connetionRecvBuffer, RecvSize, SocketFlags.None, PipeConnectionReceiveCallback, null);
                    }
                    else
                    {
                        Close();
                    }
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
                }
            }

            private void CloseSocket(ProxySocketTun sock)
            {
                lock (this)
                {
                    if (sock != null)
                    {
                        var s = sock;
                        try
                        {
                            s.Shutdown(SocketShutdown.Both);
                        }
                        catch
                        {
                            // ignored
                        }
                        try
                        {
                            s.Close();
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }

            public void Close()
            {
                lock (this)
                {
                    if (_closed)
                    {
                        return;
                    }
                    _closed = true;
                }
                ResetTimeout(0);
                Thread.Sleep(100);
                CloseSocket(_remote);
                CloseSocket(_local);
                Server.ForwardServer.Connections.DecRef(this);
            }

            public override void Shutdown()
            {
                Task.Run(Close);
            }
        }
    }
}
