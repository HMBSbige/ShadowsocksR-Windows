using Shadowsocks.Controller;
using Shadowsocks.Model;
using Shadowsocks.Obfs;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Shadowsocks.Proxy
{
    class CallbackStatus
    {
        protected int status;

        public CallbackStatus()
        {
            status = 0;
        }

        public void SetIfEqu(int newStatus, int oldStatus)
        {
            lock (this)
            {
                if (status == oldStatus)
                {
                    status = newStatus;
                }
            }
        }

        public int Status
        {
            get
            {
                lock (this)
                {
                    return status;
                }
            }
            set
            {
                lock (this)
                {
                    status = value;
                }
            }
        }
    }

    class Local : Listener.Service
    {
        private readonly Configuration _config;
        private readonly ServerTransferTotal _transfer;
        private readonly IPRangeSet _ipRange;

        public Local(Configuration config, ServerTransferTotal transfer, IPRangeSet IPRange)
        {
            _config = config;
            _transfer = transfer;
            _ipRange = IPRange;
        }

        protected bool Accept(byte[] firstPacket, int length)
        {
            if (length < 2)
            {
                return false;
            }
            if (firstPacket[0] == 5 || firstPacket[0] == 4)
            {
                return true;
            }
            if (length > 8
                && firstPacket[0] == 'C'
                && firstPacket[1] == 'O'
                && firstPacket[2] == 'N'
                && firstPacket[3] == 'N'
                && firstPacket[4] == 'E'
                && firstPacket[5] == 'C'
                && firstPacket[6] == 'T'
                && firstPacket[7] == ' ')
            {
                return true;
            }
            return false;
        }

        public bool Handle(byte[] firstPacket, int length, Socket socket)
        {
            if (!_config.GetPortMapCache().ContainsKey(((IPEndPoint)socket.LocalEndPoint).Port) && !Accept(firstPacket, length))
            {
                return false;
            }
            Task.Run(() =>
            {
                var unused = new ProxyAuthHandler(_config, _transfer, _ipRange, firstPacket, length, socket);
            });
            return true;
        }
    }

    class HandlerConfig : ICloneable
    {
        public string TargetHost;
        public int TargetPort;

        public double Ttl; // Second
        public double ConnectTimeout;
        public int TryKeepAlive;
        public string LocalDnsServers;
        public string DnsServers;
        public bool ForceLocalDnsQuery;
        // Server proxy
        public int ProxyType;
        public string Socks5RemoteHost;
        public int Socks5RemotePort;
        public string Socks5RemoteUsername;
        public string Socks5RemotePassword;
        public string ProxyUserAgent;
        // auto ban
        public bool AutoSwitchOff = true;
        // Reconnect
        public int ReconnectTimesRemain;
        public int ReconnectTimes;
        public bool Random;
        public bool ForceRandom;

        public object Clone()
        {
            var obj = new HandlerConfig
            {
                TargetHost = TargetHost,
                TargetPort = TargetPort,
                Ttl = Ttl,
                ConnectTimeout = ConnectTimeout,
                TryKeepAlive = TryKeepAlive,
                LocalDnsServers = LocalDnsServers,
                DnsServers = DnsServers,
                ForceLocalDnsQuery = ForceLocalDnsQuery,
                ProxyType = ProxyType,
                Socks5RemoteHost = Socks5RemoteHost,
                Socks5RemotePort = Socks5RemotePort,
                Socks5RemoteUsername = Socks5RemoteUsername,
                Socks5RemotePassword = Socks5RemotePassword,
                ProxyUserAgent = ProxyUserAgent,
                AutoSwitchOff = AutoSwitchOff,
                ReconnectTimesRemain = ReconnectTimesRemain,
                ReconnectTimes = ReconnectTimes,
                Random = Random,
                ForceRandom = ForceRandom
            };
            return obj;
        }
    }

    class Handler : IHandler
    {
        public delegate Server GetCurrentServer(int localPort, ServerSelectStrategy.FilterFunc filter, string targetURI = null, bool cfgRandom = false, bool usingRandom = false, bool forceRandom = false);
        public delegate void KeepCurrentServer(int localPort, string targetURI, string id);
        public GetCurrentServer getCurrentServer;
        public KeepCurrentServer keepCurrentServer;
        public Server server;
        public ServerSelectStrategy.FilterFunc select_server;
        public HandlerConfig cfg = new HandlerConfig();
        // Connection socket
        public ProxySocketTunLocal connection;
        public Socket connectionUDP;
        protected IPEndPoint connectionUDPEndPoint;
        protected int localPort;

        protected ProtocolResponseDetector detector = new ProtocolResponseDetector();
        // remote socket.
        //protected Socket remote;
        protected ProxyEncryptSocket remote;
        protected ProxyEncryptSocket remoteUDP;
        // Size of receive buffer.
        protected const int RecvSize = ProxyEncryptSocket.MSS * 4;
        protected const int BufferSize = ProxyEncryptSocket.MSS * 16;
        // remote header send buffer
        protected byte[] remoteHeaderSendBuffer;
        // connection send buffer
        protected List<byte[]> connectionSendBufferList = new List<byte[]>();

        protected DateTime lastKeepTime;
        private int _totalRecvSize;

        protected byte[] remoteUDPRecvBuffer = new byte[BufferSize];
        protected int remoteUDPRecvBufferLength;
        protected object recvUDPoverTCPLock = new object();

        protected bool closed;
        protected bool local_error;
        protected bool is_protocol_sendback;
        protected bool is_obfs_sendback;

        protected bool connectionTCPIdle, connectionUDPIdle, remoteTCPIdle, remoteUDPIdle;

        protected SpeedTester speedTester = new SpeedTester();
        protected int lastErrCode;
        protected Timer timer;
        protected object timerLock = new object();
        protected DateTime lastTimerSetTime;

        enum ConnectState
        {
            END = -1,
            READY = 0,
            HANDSHAKE = 1,
            CONNECTING = 2,
            CONNECTED = 3,
        }
        private ConnectState state = ConnectState.READY;

        private ConnectState State
        {
            get
            {
                return state;
            }
            set
            {
                lock (this)
                {
                    state = value;
                }
            }
        }

        private void ResetTimeout(double time, bool reset_keep_alive = true)
        {
            if (time <= 0 && timer == null)
                return;

            if (reset_keep_alive)
                cfg.TryKeepAlive = 0;

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
            else if (!closed)
            {
                if (lastTimerSetTime != null && (DateTime.Now - lastTimerSetTime).TotalMilliseconds > 500)
                {
                    lock (timerLock)
                    {
                        if (timer == null)
                        {
                            timer = new Timer(time * 1000.0);
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
            if (closed)
            {
                return;
            }
            bool stop = false;

            try
            {
                if (cfg.TryKeepAlive <= 0 && State == ConnectState.CONNECTED && remote != null && remoteUDP == null && remote.CanSendKeepAlive)
                {
                    cfg.TryKeepAlive++;
                    RemoteSend(remoteUDPRecvBuffer, -1);
                }
                else
                {
                    if (connection != null)
                    {
                        Server s = server;
                        if (remote != null && cfg.ReconnectTimesRemain > 0
                            //&& obfs != null && obfs.getSentLength() == 0
                            && connectionSendBufferList != null
                            && (State == ConnectState.CONNECTED || State == ConnectState.CONNECTING))
                        {
                            if (lastErrCode == 0)
                            {
                                if (State == ConnectState.CONNECTING && cfg.Socks5RemotePort > 0)
                                {
                                }
                                else
                                {
                                    lastErrCode = 8;
                                    s.ServerSpeedLog().AddTimeoutTimes();
                                }
                            }
                            //remote.Shutdown(SocketShutdown.Both);
                            stop = true;
                        }
                        else
                        {
                            if (s != null
                                && connectionSendBufferList != null
                                )
                            {
                                if (lastErrCode == 0)
                                {
                                    lastErrCode = 8;
                                    s.ServerSpeedLog().AddTimeoutTimes();
                                }
                            }
                            //connection.Shutdown(SocketShutdown.Both);
                            stop = true;
                            local_error = true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                //
            }
            if (stop)
            {
                //Thread.Sleep(200);
                Close();
            }
        }

        public void setServerTransferTotal(ServerTransferTotal transfer)
        {
            speedTester.transfer = transfer;
        }

        public int LogSocketException(Exception e)
        {
            // just log useful exceptions, not all of them
            Server s = server;
            if (e is ObfsException)
            {
                ObfsException oe = (ObfsException)e;
                if (lastErrCode == 0)
                {
                    if (s != null)
                    {
                        lastErrCode = 16;
                        s.ServerSpeedLog().AddErrorDecodeTimes();
                    }
                }
                return 16; // ObfsException(decrypt error)
            }
            else if (e is ProtocolException)
            {
                ProtocolException pe = (ProtocolException)e;
                if (lastErrCode == 0)
                {
                    if (s != null)
                    {
                        lastErrCode = 16;
                        s.ServerSpeedLog().AddErrorDecodeTimes();
                    }
                }
                return 16; // ObfsException(decrypt error)
            }
            else if (e is SocketException)
            {
                SocketException se = (SocketException)e;
                if (se.SocketErrorCode == SocketError.ConnectionAborted
                    || se.SocketErrorCode == SocketError.ConnectionReset
                    || se.SocketErrorCode == SocketError.NotConnected
                    || se.SocketErrorCode == SocketError.Interrupted
                    || se.SocketErrorCode == SocketError.Shutdown
                    )
                {
                    // closed by browser when sending
                    // normally happens when download is canceled or a tab is closed before page is loaded
                }
                else if (se.ErrorCode == 11004)
                {
                    if (lastErrCode == 0)
                    {
                        if (s != null)
                        {
                            lastErrCode = 1;
                            s.ServerSpeedLog().AddErrorTimes();
                        }
                    }
                    return 1; // proxy DNS error
                }
                else if (se.SocketErrorCode == SocketError.HostNotFound)
                {
                    if (lastErrCode == 0)
                    {
                        if (s != null)
                        {
                            lastErrCode = 2;
                            s.ServerSpeedLog().AddErrorTimes();
                            if (s.ServerSpeedLog().ErrorConnectTimes >= 3 && cfg.AutoSwitchOff)
                            {
                                s.setEnable(false);
                            }
                        }
                    }
                    return 2; // ip not exist
                }
                else if (se.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    if (lastErrCode == 0)
                    {
                        if (s != null)
                        {
                            lastErrCode = 1;
                            if (cfg != null && cfg.Socks5RemotePort == 0)
                                s.ServerSpeedLog().AddErrorTimes();
                        }
                    }
                    return 2; // proxy ip/port error
                }
                else if (se.SocketErrorCode == SocketError.NetworkUnreachable)
                {
                    if (lastErrCode == 0)
                    {
                        if (s != null)
                        {
                            lastErrCode = 3;
                            //s.ServerSpeedLog().AddErrorTimes();
                        }
                    }
                    return 3; // proxy ip/port error
                }
                else if (se.SocketErrorCode == SocketError.TimedOut)
                {
                    if (lastErrCode == 0)
                    {
                        if (s != null)
                        {
                            lastErrCode = 8;
                            s.ServerSpeedLog().AddTimeoutTimes();
                        }
                    }
                    return 8; // proxy server no response too slow
                }
                else
                {
                    if (lastErrCode == 0)
                    {
                        lastErrCode = -1;
                        if (s != null)
                            s.ServerSpeedLog().AddNoErrorTimes(); //?
                    }
                    return -1;
                }
            }
            return 0;
        }

        public bool ReConnect()
        {
            Logging.Debug("Reconnect " + cfg.TargetHost + ":" + cfg.TargetPort.ToString() + " " + connection.GetSocket().Handle.ToString());
            {
                Handler handler = new Handler();
                handler.getCurrentServer = getCurrentServer;
                handler.keepCurrentServer = keepCurrentServer;
                handler.select_server = select_server;
                handler.connection = connection;
                handler.connectionUDP = connectionUDP;
                handler.cfg = cfg.Clone() as HandlerConfig;
                handler.cfg.ReconnectTimesRemain = cfg.ReconnectTimesRemain - 1;
                handler.cfg.ReconnectTimes = cfg.ReconnectTimes + 1;

                handler.speedTester.transfer = speedTester.transfer;

                int total_len = 0;
                byte[] newFirstPacket = remoteHeaderSendBuffer;
                if (connectionSendBufferList != null && connectionSendBufferList.Count > 0)
                {
                    foreach (byte[] data in connectionSendBufferList)
                    {
                        total_len += data.Length;
                    }
                    newFirstPacket = new byte[total_len];
                    total_len = 0;
                    foreach (byte[] data in connectionSendBufferList)
                    {
                        Buffer.BlockCopy(data, 0, newFirstPacket, total_len, data.Length);
                        total_len += data.Length;
                    }
                }
                handler.Start(newFirstPacket, newFirstPacket.Length, connection.local_sendback_protocol);
            }
            return true;
        }

        public void Start(byte[] firstPacket, int length, string rsp_protocol)
        {
            connection.local_sendback_protocol = rsp_protocol;
            if (cfg.Socks5RemotePort > 0)
            {
                cfg.AutoSwitchOff = false;
            }
            ResetTimeout(cfg.Ttl);
            if (State == ConnectState.READY)
            {
                State = ConnectState.HANDSHAKE;
                remoteHeaderSendBuffer = firstPacket;

                detector.OnSend(remoteHeaderSendBuffer, length);
                byte[] data = new byte[length];
                Array.Copy(remoteHeaderSendBuffer, data, data.Length);
                connectionSendBufferList.Add(data);
                remoteHeaderSendBuffer = data;

                if (cfg.ReconnectTimes > 0)
                {
                    Task.Run(Connect);
                }
                else
                {
                    Connect();
                }
            }
            else
            {
                Close();
            }
        }

        private void BeginConnect(IPAddress ipAddress, int serverPort)
        {
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, serverPort);

            if (cfg.Socks5RemotePort != 0
                || connectionUDP == null
                || connectionUDP != null && server.udp_over_tcp)
            {
                remote = new ProxyEncryptSocket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);
                remote.GetSocket().NoDelay = true;
                try
                {
                    remote.CreateEncryptor(server.method, server.password);
                }
                catch
                {

                }
                remote.SetProtocol(ObfsFactory.GetObfs(server.protocol));
                remote.SetObfs(ObfsFactory.GetObfs(server.obfs));
            }

            if (connectionUDP != null && !server.udp_over_tcp)
            {
                try
                {
                    remoteUDP = new ProxyEncryptSocket(ipAddress.AddressFamily,
                        SocketType.Dgram, ProtocolType.Udp);
                    remoteUDP.GetSocket().Bind(new IPEndPoint(ipAddress.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0));

                    remoteUDP.CreateEncryptor(server.method, server.password);
                    remoteUDP.SetProtocol(ObfsFactory.GetObfs(server.protocol));
                    remoteUDP.SetObfs(ObfsFactory.GetObfs(server.obfs));
                    if (server.server_udp_port == 0 || cfg.Socks5RemotePort != 0)
                    {
                        IPEndPoint _remoteEP = new IPEndPoint(ipAddress, serverPort);
                        remoteUDP.SetUdpEndPoint(_remoteEP);
                    }
                    else
                    {
                        IPEndPoint _remoteEP = new IPEndPoint(ipAddress, server.server_udp_port);
                        remoteUDP.SetUdpEndPoint(_remoteEP);
                    }
                }
                catch (SocketException)
                {
                    remoteUDP = null;
                }
            }
            ResetTimeout(cfg.Ttl);

            // Connect to the remote endpoint.
            if (cfg.Socks5RemotePort == 0 && connectionUDP != null && !server.udp_over_tcp)
            {
                ConnectState _state = State;
                if (_state == ConnectState.CONNECTING)
                {
                    StartPipe();
                }
            }
            else
            {
                speedTester.BeginConnect();
                IAsyncResult result = remote.BeginConnect(remoteEP,
                    new AsyncCallback(ConnectCallback), new CallbackStatus());
                double t = cfg.ConnectTimeout <= 0 ? 30 : cfg.ConnectTimeout;
                bool success = result.AsyncWaitHandle.WaitOne((int)(t * 1000), true);
                if (!success)
                {
                    ((CallbackStatus)result.AsyncState).SetIfEqu(-1, 0);
                    if (((CallbackStatus)result.AsyncState).Status == -1)
                    {
                        if (lastErrCode == 0)
                        {
                            lastErrCode = 8;
                            server.ServerSpeedLog().AddTimeoutTimes();
                        }
                        CloseSocket(ref remote);
                        Close();
                    }
                }
            }
        }

        public bool TryReconnect()
        {
            if (local_error)
                return false;
            if (cfg.ReconnectTimesRemain > 0)
            {
                if (State == ConnectState.CONNECTING)
                {
                    return ReConnect();
                }
                else if (State == ConnectState.CONNECTED && lastErrCode == 8)
                {
                    if (connectionSendBufferList != null)
                    {
                        return ReConnect();
                    }
                }
            }
            return false;
        }

        private void CloseSocket(ref Socket sock)
        {
            lock (this)
            {
                if (sock != null)
                {
                    Socket s = sock;
                    sock = null;
                    try
                    {
                        s.Shutdown(SocketShutdown.Both);
                    }
                    catch { }
                    try
                    {
                        s.Close();
                    }
                    catch { }
                }
            }
        }

        private void CloseSocket(ref ProxySocketTunLocal sock)
        {
            lock (this)
            {
                if (sock != null)
                {
                    ProxySocketTunLocal s = sock;
                    sock = null;
                    try
                    {
                        s.Shutdown(SocketShutdown.Both);
                    }
                    catch { }
                    try
                    {
                        s.Close();
                    }
                    catch { }
                }
            }
        }

        private void CloseSocket(ref ProxyEncryptSocket sock)
        {
            lock (this)
            {
                if (sock != null)
                {
                    ProxyEncryptSocket s = sock;
                    sock = null;
                    try
                    {
                        s.Shutdown(SocketShutdown.Both);
                    }
                    catch { }
                    try
                    {
                        s.Close();
                    }
                    catch { }
                }
            }
        }

        public override void Shutdown()
        {
            Task.Run(Close);
        }

        public void Close()
        {
            lock (this)
            {
                if (closed)
                {
                    return;
                }
                closed = true;
            }
            Thread.Sleep(200);
            CloseSocket(ref remote);
            CloseSocket(ref remoteUDP);
            if (connection != null && cfg != null && connection.GetSocket() != null)
            {
                Logging.Debug("Close   " + cfg.TargetHost + ":" + cfg.TargetPort.ToString() + " " + connection.GetSocket().Handle.ToString());
            }
            if (lastErrCode == 0 && server != null && speedTester != null)
            {
                if (!local_error && speedTester.sizeProtocolRecv == 0 && speedTester.sizeUpload > 0)
                {
                    if (is_protocol_sendback
                        || (is_obfs_sendback && speedTester.sizeDownload == 0))
                    {
                        lastErrCode = 16;
                        server.ServerSpeedLog().AddErrorDecodeTimes();
                    }
                    else
                        server.ServerSpeedLog().AddErrorEmptyTimes();
                }
                else
                    server.ServerSpeedLog().AddNoErrorTimes();
            }

            if (lastErrCode == 0 && server != null && cfg != null && keepCurrentServer != null)
                keepCurrentServer(localPort, cfg.TargetHost, server.id);

            ResetTimeout(0);
            try
            {
                bool reconnect = TryReconnect();
                //lock (this)
                {
                    if (State != ConnectState.END)
                    {
                        if (State != ConnectState.READY && State != ConnectState.HANDSHAKE && server != null)
                        {
                            if (server.GetConnections().DecRef(this))
                            {
                                server.ServerSpeedLog().AddDisconnectTimes();
                            }
                        }
                        State = ConnectState.END;
                    }
                }

                if (!reconnect)
                {
                    Logging.Info($"Disconnect {cfg.TargetHost}:{cfg.TargetPort.ToString()}");
                    CloseSocket(ref connection);
                    CloseSocket(ref connectionUDP);
                    Logging.Debug($"Transfer {cfg.TargetHost}:{cfg.TargetPort.ToString() + speedTester.TransferLog()}");
                }
                else
                {
                    connection = null;
                    connectionUDP = null;
                }
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
            }

            getCurrentServer = null;
            keepCurrentServer = null;

            detector = null;
            speedTester = null;
            remoteUDPRecvBuffer = null;

            server = null;
            select_server = null;

            cfg = null;
        }

        private bool ConnectProxyServer(string strRemoteHost, int iRemotePort)
        {
            if (cfg.ProxyType == 0)
            {
                bool ret = remote.ConnectSocks5ProxyServer(strRemoteHost, iRemotePort, connectionUDP != null && !server.udp_over_tcp, cfg.Socks5RemoteUsername, cfg.Socks5RemotePassword);
                remote.SetTcpServer(server.server, server.server_port);
                remote.SetUdpServer(server.server, server.server_udp_port == 0 ? server.server_port : server.server_udp_port);
                if (remoteUDP != null)
                {
                    remoteUDP.GoS5Proxy = true;
                    remoteUDP.SetUdpServer(server.server, server.server_udp_port == 0 ? server.server_port : server.server_udp_port);
                    remoteUDP.SetUdpEndPoint(remote.GetProxyUdpEndPoint());
                }
                return ret;
            }
            else if (cfg.ProxyType == 1)
            {
                bool ret = remote.ConnectHttpProxyServer(strRemoteHost, iRemotePort, cfg.Socks5RemoteUsername, cfg.Socks5RemotePassword, cfg.ProxyUserAgent);
                remote.SetTcpServer(server.server, server.server_port);
                return ret;
            }
            else
            {
                return true;
            }
        }

        private void Connect()
        {
            remote = null;
            remoteUDP = null;
            localPort = ((IPEndPoint)connection.GetSocket().LocalEndPoint).Port;
            if (select_server == null)
            {
                if (cfg.TargetHost == null)
                {
                    cfg.TargetHost = GetQueryString();
                    cfg.TargetPort = GetQueryPort();
                    server = getCurrentServer(localPort, null, cfg.TargetHost, cfg.Random, true);
                }
                else
                {
                    server = getCurrentServer(localPort, null, cfg.TargetHost, cfg.Random, true, cfg.ForceRandom);
                }
            }
            else
            {
                if (cfg.TargetHost == null)
                {
                    cfg.TargetHost = GetQueryString();
                    cfg.TargetPort = GetQueryPort();
                    server = getCurrentServer(localPort, select_server, cfg.TargetHost, true, true);
                }
                else
                {
                    server = getCurrentServer(localPort, select_server, cfg.TargetHost, true, true, cfg.ForceRandom);
                }
            }
            speedTester.server = server.server;
            Logging.Info($@"Connect {cfg.TargetHost}:{cfg.TargetPort.ToString()} via {server.server}:{server.server_port}");

            ResetTimeout(cfg.Ttl);
            if (cfg.TargetHost != null)
            {
                var host = cfg.TargetHost;

                if (!IPAddress.TryParse(host, out var ipAddress))
                {
                    ipAddress = Utils.DnsBuffer.Get(host) ?? Utils.QueryDns(host, host.IndexOf('.') >= 0 ? cfg.DnsServers : null);
                    if (ipAddress != null)
                    {
                        Logging.Info($@"DNS nolock query {host} answer {ipAddress}");
                        Utils.DnsBuffer.Set(host, new IPAddress(ipAddress.GetAddressBytes()));
                        Utils.DnsBuffer.Sweep();
                    }
                    else
                    {
                        Logging.Info($@"DNS nolock query {host} failed.");
                    }
                }

                if (ipAddress != null)
                {
                    cfg.TargetHost = ipAddress.ToString();
                    ResetTimeout(cfg.Ttl);
                }
            }

            lock (this)
            {
                server.ServerSpeedLog().AddConnectTimes();
                if (State == ConnectState.HANDSHAKE)
                {
                    State = ConnectState.CONNECTING;
                }
                server.GetConnections().AddRef(this);
            }
            try
            {
                var serverHost = server.server;
                int serverPort = server.server_port;
                if (cfg.Socks5RemotePort > 0)
                {
                    serverHost = cfg.Socks5RemoteHost;
                    serverPort = cfg.Socks5RemotePort;
                }
                if (!IPAddress.TryParse(serverHost, out var ipAddress))
                {
                    if (server.ServerSpeedLog().ErrorContinurousTimes > 10)
                    {
                        server.DnsBuffer().force_expired = true;
                    }

                    if (server.DnsBuffer().isExpired(serverHost))
                    {
                        var dnsOk = false;
                        var buf = server.DnsBuffer();
                        if (Monitor.TryEnter(buf, buf.ip != null ? 100 : 1000000))
                        {
                            if (buf.isExpired(serverHost))
                            {
                                ipAddress = Utils.QueryDns(serverHost, serverHost.IndexOf('.') >= 0 ? cfg.LocalDnsServers : null);

                                if (ipAddress != null)
                                {
                                    buf.UpdateDns(serverHost, ipAddress);
                                    dnsOk = true;
                                }
                            }
                            else
                            {
                                ipAddress = buf.ip;
                                dnsOk = true;
                            }

                            Monitor.Exit(buf);
                        }
                        else
                        {
                            if (buf.ip != null)
                            {
                                ipAddress = buf.ip;
                                dnsOk = true;
                            }
                        }

                        if (!dnsOk)
                        {
                            if (server.DnsBuffer().ip != null)
                            {
                                ipAddress = server.DnsBuffer().ip;
                            }
                            else
                            {
                                lastErrCode = 8;
                                server.ServerSpeedLog().AddTimeoutTimes();
                                Close();
                                return;
                            }
                        }
                    }
                    else
                    {
                        ipAddress = server.DnsBuffer().ip;
                    }
                }
                BeginConnect(ipAddress, serverPort);
            }
            catch (Exception e)
            {
                LogException(e);
                Close();
            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            if (ar != null && ar.AsyncState != null)
            {
                ((CallbackStatus)ar.AsyncState).SetIfEqu(1, 0);
                if (((CallbackStatus)ar.AsyncState).Status != 1)
                    return;
            }
            try
            {
                remote.EndConnect(ar);
                if (cfg.Socks5RemotePort > 0)
                {
                    if (!ConnectProxyServer(server.server, server.server_port))
                    {
                        throw new SocketException((int)SocketError.ConnectionReset);
                    }
                }
                speedTester.EndConnect();

                ConnectState _state = State;
                if (_state == ConnectState.CONNECTING)
                {
                    StartPipe();
                }
            }
            catch (Exception e)
            {
                LogExceptionAndClose(e);
            }
        }

        // do/end xxx tcp/udp Recv
        private void doConnectionTCPRecv()
        {
            if (connection != null && connectionTCPIdle)
            {
                connectionTCPIdle = false;
                int recv_size = remote == null ? RecvSize : remote.TcpMSS - remote.OverHead;
                byte[] buffer = new byte[recv_size];
                connection.BeginReceive(buffer, recv_size, 0,
                    new AsyncCallback(PipeConnectionReceiveCallback), null);
            }
        }

        private int endConnectionTCPRecv(IAsyncResult ar)
        {
            if (connection != null)
            {
                int bytesRead = connection.EndReceive(ar);
                connectionTCPIdle = true;
                return bytesRead;
            }
            return 0;
        }

        private void doConnectionUDPRecv()
        {
            if (connectionUDP != null && connectionUDPIdle)
            {
                connectionUDPIdle = false;
                const int BufferSize = 65536;
                IPEndPoint sender = new IPEndPoint(connectionUDP.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
                EndPoint tempEP = (EndPoint)sender;
                byte[] buffer = new byte[BufferSize];
                connectionUDP.BeginReceiveFrom(buffer, 0, BufferSize, SocketFlags.None, ref tempEP,
                    new AsyncCallback(PipeConnectionUDPReceiveCallback), buffer);
            }
        }

        private int endConnectionUDPRecv(IAsyncResult ar, ref EndPoint endPoint)
        {
            if (connectionUDP != null)
            {
                int bytesRead = connectionUDP.EndReceiveFrom(ar, ref endPoint);
                if (connectionUDPEndPoint == null)
                    connectionUDPEndPoint = (IPEndPoint)endPoint;
                connectionUDPIdle = true;
                return bytesRead;
            }
            return 0;
        }

        private void doRemoteTCPRecv()
        {
            if (remote != null && remoteTCPIdle)
            {
                remoteTCPIdle = false;
                remote.BeginReceive(new byte[BufferSize], RecvSize, 0,
                    new AsyncCallback(PipeRemoteReceiveCallback), null);
            }
        }

        private int endRemoteTCPRecv(IAsyncResult ar)
        {
            if (remote != null)
            {
                bool sendback;
                int bytesRead = remote.EndReceive(ar, out sendback);

                int bytesRecv = remote.GetAsyncResultSize(ar);
                server.ServerSpeedLog().AddDownloadBytes(bytesRecv, DateTime.Now, speedTester.AddDownloadSize(bytesRecv));

                if (sendback)
                {
                    RemoteSend(remoteUDPRecvBuffer, 0);
                    doConnectionRecv();
                }
                remoteTCPIdle = true;
                return bytesRead;
            }
            return 0;
        }

        private void doRemoteUDPRecv()
        {
            if (remoteUDP != null && remoteUDPIdle)
            {
                remoteUDPIdle = false;
                const int BufferSize = 65536;
                IPEndPoint sender = new IPEndPoint(remoteUDP.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
                EndPoint tempEP = (EndPoint)sender;
                remoteUDP.BeginReceiveFrom(new byte[BufferSize], BufferSize, SocketFlags.None, ref tempEP,
                    new AsyncCallback(PipeRemoteUDPReceiveCallback), null);
            }
        }

        private int endRemoteUDPRecv(IAsyncResult ar, ref EndPoint endPoint)
        {
            if (remoteUDP != null)
            {
                int bytesRead = remoteUDP.EndReceiveFrom(ar, ref endPoint);
                remoteUDPIdle = true;
                return bytesRead;
            }
            return 0;
        }

        private void doConnectionRecv()
        {
            doConnectionTCPRecv();
            doConnectionUDPRecv();
        }

        private void SetObfsPlugin()
        {
            int head_len = 30;
            if (connectionSendBufferList != null && connectionSendBufferList.Count > 0)
            {
                head_len = ObfsBase.GetHeadSize(connectionSendBufferList[0], 30);
            }
            else
            {
                head_len = ObfsBase.GetHeadSize(remoteHeaderSendBuffer, 30);
            }
            if (remote != null) remote.SetObfsPlugin(server, head_len);
            if (remoteUDP != null) remoteUDP.SetObfsPlugin(server, head_len);
        }

        private string GetQueryString()
        {
            if (remoteHeaderSendBuffer == null)
                return null;

            if (remoteHeaderSendBuffer[0] == 1)
            {
                if (remoteHeaderSendBuffer.Length > 4)
                {
                    byte[] addr = new byte[4];
                    Array.Copy(remoteHeaderSendBuffer, 1, addr, 0, 4);
                    IPAddress ipAddress = new IPAddress(addr);
                    return ipAddress.ToString();
                }
                return null;
            }
            if (remoteHeaderSendBuffer[0] == 4)
            {
                if (remoteHeaderSendBuffer.Length > 16)
                {
                    byte[] addr = new byte[16];
                    Array.Copy(remoteHeaderSendBuffer, 1, addr, 0, 16);
                    IPAddress ipAddress = new IPAddress(addr);
                    return ipAddress.ToString();
                }
                return null;
            }
            if (remoteHeaderSendBuffer[0] == 3 && remoteHeaderSendBuffer.Length > 1)
            {
                if (remoteHeaderSendBuffer.Length > remoteHeaderSendBuffer[1] + 1)
                {
                    string url = System.Text.Encoding.UTF8.GetString(remoteHeaderSendBuffer, 2, remoteHeaderSendBuffer[1]);
                    return url;
                }
            }
            return null;
        }

        private int GetQueryPort()
        {
            if (remoteHeaderSendBuffer == null)
                return 0;

            if (remoteHeaderSendBuffer[0] == 1)
            {
                if (remoteHeaderSendBuffer.Length > 6)
                {
                    int port = (remoteHeaderSendBuffer[5] << 8) | remoteHeaderSendBuffer[6];
                    return port;
                }
                return 0;
            }
            if (remoteHeaderSendBuffer[0] == 4)
            {
                if (remoteHeaderSendBuffer.Length > 18)
                {
                    int port = (remoteHeaderSendBuffer[17] << 8) | remoteHeaderSendBuffer[18];
                    return port;
                }
                return 0;
            }
            if (remoteHeaderSendBuffer[0] == 3 && remoteHeaderSendBuffer.Length > 1)
            {
                if (remoteHeaderSendBuffer.Length > remoteHeaderSendBuffer[1] + 2)
                {
                    int port = (remoteHeaderSendBuffer[remoteHeaderSendBuffer[1] + 2] << 8) | remoteHeaderSendBuffer[remoteHeaderSendBuffer[1] + 3];
                    return port;
                }
            }
            return 0;
        }

        // 2 sides connection start
        private void StartPipe()
        {
            try
            {
                // set mark
                connectionTCPIdle = true;
                connectionUDPIdle = true;
                remoteTCPIdle = true;
                remoteUDPIdle = true;
                closed = false;

                remoteUDPRecvBufferLength = 0;
                SetObfsPlugin();

                ResetTimeout(cfg.ConnectTimeout);

                speedTester.BeginUpload();

                // remote ready
                if (connectionUDP == null) // TCP
                {
                    if (cfg.ReconnectTimes > 0 || cfg.TargetPort != 0)
                    {
                        RemoteSend(remoteHeaderSendBuffer, remoteHeaderSendBuffer.Length);
                        remoteHeaderSendBuffer = null;
                    }

                    is_protocol_sendback = remote.isProtocolSendback;
                    is_obfs_sendback = remote.isObfsSendback;
                }
                else // UDP
                {
                    if (!server.udp_over_tcp &&
                        remoteUDP != null)
                    {
                        if (cfg.Socks5RemotePort == 0)
                            CloseSocket(ref remote);
                        remoteHeaderSendBuffer = null;
                    }
                    else if (remoteHeaderSendBuffer != null)
                    {
                        RemoteSend(remoteHeaderSendBuffer, remoteHeaderSendBuffer.Length);
                        remoteHeaderSendBuffer = null;
                    }
                }
                State = ConnectState.CONNECTED;

                if (connection.local_sendback_protocol != null)
                {
                    connection.Send(remoteUDPRecvBuffer, 0, 0);
                }

                // remote recv first
                doRemoteTCPRecv();
                doRemoteUDPRecv();

                doConnectionTCPRecv();
                doConnectionUDPRecv();
            }
            catch (Exception e)
            {
                LogExceptionAndClose(e);
            }
        }

        private void ConnectionSend(byte[] buffer, int bytesToSend)
        {
            if (connectionUDP == null)
            {
                connection.Send(buffer, bytesToSend, SocketFlags.None);
                doRemoteUDPRecv();
            }
            else
            {
                connectionUDP.BeginSendTo(buffer, 0, bytesToSend, SocketFlags.None, connectionUDPEndPoint, new AsyncCallback(PipeConnectionUDPSendCallback), null);
            }
        }

        private void UDPoverTCPConnectionSend(byte[] send_buffer, int bytesToSend)
        {
            List<byte[]> buffer_list = new List<byte[]>();
            lock (recvUDPoverTCPLock)
            {
                Utils.SetArrayMinSize(ref remoteUDPRecvBuffer, bytesToSend + remoteUDPRecvBufferLength);
                Array.Copy(send_buffer, 0, remoteUDPRecvBuffer, remoteUDPRecvBufferLength, bytesToSend);
                remoteUDPRecvBufferLength += bytesToSend;
                while (remoteUDPRecvBufferLength > 6)
                {
                    int len = (remoteUDPRecvBuffer[0] << 8) + remoteUDPRecvBuffer[1];
                    if (len > remoteUDPRecvBufferLength)
                        break;

                    byte[] buffer = new byte[len];
                    Array.Copy(remoteUDPRecvBuffer, buffer, len);
                    remoteUDPRecvBufferLength -= len;
                    Array.Copy(remoteUDPRecvBuffer, len, remoteUDPRecvBuffer, 0, remoteUDPRecvBufferLength);

                    buffer[0] = 0;
                    buffer[1] = 0;
                    buffer_list.Add(buffer);
                }
            }
            if (buffer_list.Count == 0)
            {
                doRemoteTCPRecv();
            }
            else
            {
                foreach (byte[] buffer in buffer_list)
                {
                    if (buffer == buffer_list[buffer_list.Count - 1])
                        connectionUDP.BeginSendTo(buffer, 0, buffer.Length, SocketFlags.None, connectionUDPEndPoint, new AsyncCallback(PipeConnectionUDPSendCallback), null);
                    else
                        connectionUDP.BeginSendTo(buffer, 0, buffer.Length, SocketFlags.None, connectionUDPEndPoint, new AsyncCallback(PipeConnectionUDPSendCallbackNoRecv), null);
                }
            }
        }

        private void PipeRemoteReceiveCallback(IAsyncResult ar)
        {
            bool final_close = false;
            try
            {
                if (closed)
                {
                    return;
                }
                int bytesRead = endRemoteTCPRecv(ar);

                if (remote.IsClose)
                {
                    final_close = true;
                }
                else
                {
                    int bytesRecv = remote.GetAsyncResultSize(ar);
                    if (speedTester.BeginDownload())
                    {
                        int pingTime = -1;
                        if (speedTester.timeBeginDownload != null && speedTester.timeBeginUpload != null)
                            pingTime = (int)(speedTester.timeBeginDownload - speedTester.timeBeginUpload).TotalMilliseconds;
                        if (pingTime >= 0)
                            server.ServerSpeedLog().AddConnectTime(pingTime);
                    }
                    ResetTimeout(cfg.Ttl);

                    speedTester.AddProtocolRecvSize(remote.GetAsyncProtocolSize(ar));
                    if (bytesRead > 0)
                    {
                        byte[] remoteSendBuffer = new byte[BufferSize];

                        Array.Copy(remote.GetAsyncResultBuffer(ar), remoteSendBuffer, bytesRead);
                        if (connectionUDP == null)
                        {
                            if (detector.OnRecv(remoteSendBuffer, bytesRead) > 0)
                            {
                                server.ServerSpeedLog().AddErrorTimes();
                            }
                            if (detector.Pass)
                            {
                                server.ServerSpeedLog().ResetErrorDecodeTimes();
                            }
                            else
                            {
                                server.ServerSpeedLog().ResetEmptyTimes();
                            }
                            connection.Send(remoteSendBuffer, bytesRead, 0);
                        }
                        else
                        {
                            UDPoverTCPConnectionSend(remoteSendBuffer, bytesRead);
                        }
                        server.ServerSpeedLog().AddDownloadRawBytes(bytesRead);
                        speedTester.AddRecvSize(bytesRead);
                        _totalRecvSize += bytesRead;
                    }
                    if (connectionUDP == null && _totalRecvSize > 1024 * 1024 * 2)
                    {
                        PipeRemoteReceiveLoop();
                    }
                    else
                    {
                        doRemoteTCPRecv();
                    }
                }
            }
            catch (Exception e)
            {
                LogException(e);
                final_close = true;
            }
            finally
            {
                if (final_close)
                {
                    Close();
                }
            }
        }

        private void PipeRemoteReceiveLoop()
        {
            bool final_close = false;
            byte[] recv_buffer = new byte[BufferSize * 4];

            DateTime beforeReceive = DateTime.Now;
            while (!closed)
            {
                try
                {
                    int protocolSize;
                    bool sendback;
                    int bytesRecv;
                    int bytesRead = remote.Receive(recv_buffer, RecvSize, 0, out bytesRecv, out protocolSize, out sendback);
                    DateTime now = DateTime.Now;
                    if (remote != null && remote.IsClose)
                    {
                        final_close = true;
                        break;
                    }
                    if (closed)
                    {
                        break;
                    }
                    if (speedTester.BeginDownload())
                    {
                        int pingTime = -1;
                        if (speedTester.timeBeginDownload != null && speedTester.timeBeginUpload != null)
                            pingTime = (int)(speedTester.timeBeginDownload - speedTester.timeBeginUpload).TotalMilliseconds;
                        if (pingTime >= 0)
                            server.ServerSpeedLog().AddConnectTime(pingTime);
                    }
                    server.ServerSpeedLog().AddDownloadBytes(bytesRecv, now, speedTester.AddDownloadSize(bytesRecv));
                    ResetTimeout(cfg.Ttl);
                    if (sendback)
                    {
                        RemoteSend(remoteUDPRecvBuffer, 0);
                        doConnectionRecv();
                    }

                    if (bytesRead > 0)
                    {
                        byte[] remoteSendBuffer = new byte[BufferSize];

                        Array.Copy(recv_buffer, remoteSendBuffer, bytesRead);
                        if (connectionUDP == null)
                        {
                            if (detector.OnRecv(remoteSendBuffer, bytesRead) > 0)
                            {
                                server.ServerSpeedLog().AddErrorTimes();
                            }
                            if (detector.Pass)
                            {
                                server.ServerSpeedLog().ResetErrorDecodeTimes();
                            }
                            else
                            {
                                server.ServerSpeedLog().ResetEmptyTimes();
                            }
                            connection.Send(remoteSendBuffer, bytesRead, 0);
                        }
                        else
                        {
                            UDPoverTCPConnectionSend(remoteSendBuffer, bytesRead);
                        }
                        speedTester.AddProtocolRecvSize(protocolSize);
                        server.ServerSpeedLog().AddDownloadRawBytes(bytesRead);
                        speedTester.AddRecvSize(bytesRead);
                    }

                    if ((now - beforeReceive).TotalSeconds > 5)
                    {
                        _totalRecvSize = 0;
                        doRemoteTCPRecv();
                        return;
                    }
                    else
                    {
                        beforeReceive = now;
                    }
                }
                catch (Exception e)
                {
                    LogException(e);
                    final_close = true;
                    break;
                }
            }
            if (final_close)
                Close();
        }

        // end ReceiveCallback
        private void PipeRemoteUDPReceiveCallback(IAsyncResult ar)
        {
            bool final_close = false;
            try
            {
                if (closed)
                {
                    return;
                }
                IPEndPoint sender = new IPEndPoint(remoteUDP.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
                EndPoint tempEP = (EndPoint)sender;

                int bytesRead = endRemoteUDPRecv(ar, ref tempEP);

                if (remoteUDP.IsClose)
                {
                    final_close = true;
                }
                else
                {
                    int bytesRecv = remoteUDP.GetAsyncResultSize(ar);
                    if (speedTester.BeginDownload())
                    {
                        int pingTime = -1;
                        if (speedTester.timeBeginDownload != null && speedTester.timeBeginUpload != null)
                            pingTime = (int)(speedTester.timeBeginDownload - speedTester.timeBeginUpload).TotalMilliseconds;
                        if (pingTime >= 0)
                            server.ServerSpeedLog().AddConnectTime(pingTime);
                    }
                    server.ServerSpeedLog().AddDownloadBytes(bytesRecv, DateTime.Now, speedTester.AddDownloadSize(bytesRecv));
                    ResetTimeout(cfg.Ttl);

                    if (bytesRead <= 0)
                    {
                        doRemoteUDPRecv();
                    }
                    else //if (bytesRead > 0)
                    {
                        ConnectionSend(remoteUDP.GetAsyncResultBuffer(ar), bytesRead);

                        speedTester.AddRecvSize(bytesRead);
                        server.ServerSpeedLog().AddDownloadRawBytes(bytesRead);
                    }
                }
            }
            catch (Exception e)
            {
                LogException(e);
                final_close = true;
            }
            finally
            {
                if (final_close)
                {
                    Close();
                }
            }
        }

        private int RemoteSend(byte[] bytes, int length)
        {
            int total_len = 0;
            int send_len;
            send_len = remote.Send(bytes, length, SocketFlags.None);
            if (send_len > 0)
            {
                server.ServerSpeedLog().AddUploadBytes(send_len, DateTime.Now, speedTester.AddUploadSize(send_len));
                if (length >= 0)
                    ResetTimeout(cfg.Ttl);
                else
                    ResetTimeout(cfg.ConnectTimeout <= 0 ? 30 : cfg.ConnectTimeout, false);
                total_len += send_len;

                if (lastKeepTime == null || (DateTime.Now - lastKeepTime).TotalSeconds > 5)
                {
                    if (keepCurrentServer != null)
                    {
                        keepCurrentServer(localPort, cfg.TargetHost, server.id);
                    }
                    lastKeepTime = DateTime.Now;
                }

                while (true)
                {
                    send_len = remote.Send(null, 0, SocketFlags.None);
                    if (send_len > 0)
                    {
                        server.ServerSpeedLog().AddUploadBytes(send_len, DateTime.Now, speedTester.AddUploadSize(send_len));
                        total_len += send_len;
                    }
                    else
                        break;
                }
            }
            return total_len;
        }

        private void RemoteSendto(byte[] bytes, int length)
        {
            int send_len;
            send_len = remoteUDP.BeginSendTo(bytes, length, SocketFlags.None, new AsyncCallback(PipeRemoteUDPSendCallback), null);
            server.ServerSpeedLog().AddUploadBytes(send_len, DateTime.Now, speedTester.AddUploadSize(send_len));
        }

        private void PipeConnectionReceiveCallback(IAsyncResult ar)
        {
            bool final_close = false;
            try
            {
                if (closed)
                {
                    return;
                }
                int bytesRead = endConnectionTCPRecv(ar);

                if (bytesRead > 0)
                {
                    if (connectionUDP != null)
                    {
                        doConnectionTCPRecv();
                        ResetTimeout(cfg.Ttl);
                        return;
                    }
                    byte[] connetionRecvBuffer = new byte[BufferSize];
                    Array.Copy((ar.AsyncState as CallbackState).buffer, 0, connetionRecvBuffer, 0, bytesRead);
                    if (connectionSendBufferList != null)
                    {
                        detector.OnSend(connetionRecvBuffer, bytesRead);
                        byte[] data = new byte[bytesRead];
                        Array.Copy(connetionRecvBuffer, data, data.Length);
                        connectionSendBufferList.Add(data);
                    }
                    if (State == ConnectState.CONNECTED)
                    {
                        if (remoteHeaderSendBuffer != null)
                        {
                            Array.Copy(connetionRecvBuffer, 0, connetionRecvBuffer, remoteHeaderSendBuffer.Length, bytesRead);
                            Array.Copy(remoteHeaderSendBuffer, 0, connetionRecvBuffer, 0, remoteHeaderSendBuffer.Length);
                            bytesRead += remoteHeaderSendBuffer.Length;
                            remoteHeaderSendBuffer = null;
                        }
                        else
                        {
                            Logging.LogBin(LogLevel.Debug, "remote send", connetionRecvBuffer, bytesRead);
                        }
                    }
                    if (speedTester.sizeRecv > 0)
                    {
                        connectionSendBufferList = null;
                        server.ServerSpeedLog().ResetContinurousTimes();
                    }
                    if (closed || State != ConnectState.CONNECTED)
                    {
                        return;
                    }
                    if (connectionSendBufferList != null)
                    {
                        ResetTimeout(cfg.ConnectTimeout);
                    }
                    else
                    {
                        ResetTimeout(cfg.Ttl);
                    }
                    int send_len = RemoteSend(connetionRecvBuffer, bytesRead);
                    if (!(send_len == 0 && bytesRead > 0))
                        doConnectionRecv();
                }
                else
                {
                    local_error = true;
                    final_close = true;
                }
            }
            catch (Exception e)
            {
                local_error = true;
                LogException(e);
                final_close = true;
            }
            finally
            {
                if (final_close)
                {
                    Close();
                }
            }
        }

        private void PipeConnectionUDPReceiveCallback(IAsyncResult ar)
        {
            bool final_close = false;
            try
            {
                if (closed)
                {
                    return;
                }
                IPEndPoint sender = new IPEndPoint(connectionUDP.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
                EndPoint tempEP = (EndPoint)sender;

                int bytesRead = endConnectionUDPRecv(ar, ref tempEP);

                if (bytesRead > 0)
                {
                    byte[] connetionSendBuffer = new byte[bytesRead];
                    Array.Copy((byte[])ar.AsyncState, connetionSendBuffer, bytesRead);
                    if (!server.udp_over_tcp && remoteUDP != null)
                    {
                        RemoteSendto(connetionSendBuffer, bytesRead);
                    }
                    else
                    {
                        if (connetionSendBuffer[0] == 0 && connetionSendBuffer[1] == 0)
                        {
                            connetionSendBuffer[0] = (byte)(bytesRead >> 8);
                            connetionSendBuffer[1] = (byte)(bytesRead);
                            RemoteSend(connetionSendBuffer, bytesRead);
                            doConnectionRecv();
                        }
                    }
                    ResetTimeout(cfg.Ttl);
                }
                else
                {
                    final_close = true;
                }
            }
            catch (Exception e)
            {
                LogException(e);
                final_close = true;
            }
            finally
            {
                if (final_close)
                {
                    Close();
                }
            }
        }

        private void PipeRemoteUDPSendCallback(IAsyncResult ar)
        {
            if (closed)
            {
                return;
            }
            try
            {
                remoteUDP.EndSendTo(ar);
                doConnectionRecv();
            }
            catch (Exception e)
            {
                LogExceptionAndClose(e);
            }
        }

        private void PipeConnectionUDPSendCallbackNoRecv(IAsyncResult ar)
        {
            if (closed)
            {
                return;
            }
            try
            {
                connectionUDP.EndSendTo(ar);
            }
            catch (Exception e)
            {
                LogExceptionAndClose(e);
            }
        }

        private void PipeConnectionUDPSendCallback(IAsyncResult ar)
        {
            if (closed)
            {
                return;
            }
            try
            {
                connectionUDP.EndSendTo(ar);
                doRemoteTCPRecv();
                doRemoteUDPRecv();
            }
            catch (Exception e)
            {
                LogExceptionAndClose(e);
            }
        }

        protected string getServerUrl(out string remarks)
        {
            Server s = server;
            if (s == null)
            {
                remarks = "";
                return "";
            }
            remarks = s.remarks;
            return s.server;
        }

        private void LogException(Exception e)
        {
            int err = LogSocketException(e);
            string remarks;
            string server_url = getServerUrl(out remarks);
            if (err != 0 && !Logging.LogSocketException(remarks, server_url, e))
                Logging.LogUsefulException(e);
        }

        private void LogExceptionAndClose(Exception e)
        {
            LogException(e);
            Close();
        }
    }

}
