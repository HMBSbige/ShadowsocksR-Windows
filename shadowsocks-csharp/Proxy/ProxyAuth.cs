using Shadowsocks.Controller;
using Shadowsocks.Controller.Service;
using Shadowsocks.Model;
using Shadowsocks.Model.Transfer;
using Shadowsocks.Util.NetUtils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static Shadowsocks.Encryption.EncryptorBase;

namespace Shadowsocks.Proxy
{
    class ProxyAuthHandler
    {
        private Configuration _config;
        private ServerTransferTotal _transfer;
        private IPRangeSet _IPRange;

        private byte[] _firstPacket;
        private int _firstPacketLength;

        private Socket _connection;
        private Socket _connectionUDP;
        private string local_sendback_protocol;

        protected const int RECV_SIZE = 16384;
        protected byte[] _connetionRecvBuffer = new byte[RECV_SIZE * 2];

        public byte command;
        protected byte[] _remoteHeaderSendBuffer;

        protected HttpParser httpProxyState;

        private const int CMD_CONNECT = 0x01;
        private const int CMD_UDP_ASSOC = 0x03;

        public ProxyAuthHandler(Configuration config, ServerTransferTotal transfer, IPRangeSet IPRange, byte[] firstPacket, int length, Socket socket)
        {
            var local_port = ((IPEndPoint)socket.LocalEndPoint).Port;

            _config = config;
            _transfer = transfer;
            _IPRange = IPRange;
            _firstPacket = firstPacket;
            _firstPacketLength = length;
            _connection = socket;
            socket.NoDelay = true;

            if (_config.GetPortMapCache().ContainsKey(local_port) && _config.GetPortMapCache()[local_port].type == PortMapType.Forward)
            {
                Connect();
            }
            else
            {
                HandshakeReceive();
            }
        }

        private void CloseSocket(ref Socket sock)
        {
            lock (this)
            {
                if (sock != null)
                {
                    var s = sock;
                    sock = null;
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

        private void Close()
        {
            CloseSocket(ref _connection);
            CloseSocket(ref _connectionUDP);

            _config = null;
        }

        private bool AuthConnection(string authUser, string authPass)
        {
            if ((_config.authUser ?? string.Empty).Length == 0)
            {
                return true;
            }
            if (_config.authUser == authUser && (_config.authPass ?? "") == authPass)
            {
                return true;
            }
            return IPSubnet.IsLoopBack(((IPEndPoint)_connection.RemoteEndPoint).Address);
        }

        private void HandshakeReceive()
        {
            try
            {
                var bytesRead = _firstPacketLength;

                if (bytesRead > 1)
                {
                    if ((!string.IsNullOrEmpty(_config.authUser) || IPSubnet.IsLoopBack(((IPEndPoint)_connection.RemoteEndPoint).Address))
                        && _firstPacket[0] == 4 && _firstPacketLength >= 9)
                    {
                        RspSocks4aHandshakeReceive();
                    }
                    else if (_firstPacket[0] == 5 && _firstPacketLength >= 3)
                    {
                        RspSocks5HandshakeReceive();
                    }
                    else
                    {
                        RspHttpHandshakeReceive();
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

        private void RspSocks4aHandshakeReceive()
        {
            var firstPacket = new List<byte>();
            for (var i = 0; i < _firstPacketLength; ++i)
            {
                firstPacket.Add(_firstPacket[i]);
            }
            var dataSockSend = firstPacket.GetRange(0, 4);
            dataSockSend[0] = 0;
            dataSockSend[1] = 90;

            var remoteDNS = _firstPacket[4] == 0 && _firstPacket[5] == 0 && _firstPacket[6] == 0 && _firstPacket[7] == 1;
            if (remoteDNS)
            {
                for (var i = 0; i < 4; ++i)
                {
                    dataSockSend.Add(0);
                }
                var addrStartPos = firstPacket.IndexOf(0x0, 8);
                var addr = firstPacket.GetRange(addrStartPos + 1, firstPacket.Count - addrStartPos - 2);
                _remoteHeaderSendBuffer = new byte[2 + addr.Count + 2];
                _remoteHeaderSendBuffer[0] = 3;
                _remoteHeaderSendBuffer[1] = (byte)addr.Count;
                Array.Copy(addr.ToArray(), 0, _remoteHeaderSendBuffer, 2, addr.Count);
                _remoteHeaderSendBuffer[2 + addr.Count] = dataSockSend[2];
                _remoteHeaderSendBuffer[2 + addr.Count + 1] = dataSockSend[3];
            }
            else
            {
                for (var i = 0; i < 4; ++i)
                {
                    dataSockSend.Add(_firstPacket[4 + i]);
                }
                _remoteHeaderSendBuffer = new byte[1 + 4 + 2];
                _remoteHeaderSendBuffer[0] = 1;
                Array.Copy(dataSockSend.ToArray(), 4, _remoteHeaderSendBuffer, 1, 4);
                _remoteHeaderSendBuffer[1 + 4] = dataSockSend[2];
                _remoteHeaderSendBuffer[1 + 4 + 1] = dataSockSend[3];
            }
            command = 1; // Set TCP connect command
            _connection.Send(dataSockSend.ToArray());
            Connect();
        }

        private void RspSocks5HandshakeReceive()
        {
            byte[] response = { 5, 0 };
            if (_firstPacket[0] != 5)
            {
                response = new byte[] { 0, 91 };
                Console.WriteLine(@"socks 4/5 protocol error");
                _connection.Send(response);
                Close();
                return;
            }

            var auth = false;
            var has_method = false;
            for (var index = 0; index < _firstPacket[1]; ++index)
            {
                if (_firstPacket[2 + index] == 0)
                {
                    has_method = true;
                }
                else if (_firstPacket[2 + index] == 2)
                {
                    auth = true;
                    has_method = true;
                }
            }
            if (!has_method)
            {
                Console.WriteLine(@"Socks5 no acceptable auth method");
                Close();
                return;
            }
            if (auth)
            {
                response[1] = 2;
                _connection.BeginSend(response, 0, response.Length, SocketFlags.None, HandshakeAuthSendCallback, null);
            }
            else if (string.IsNullOrEmpty(_config.authUser)
                     || IPSubnet.IsLoopBack(((IPEndPoint)_connection.RemoteEndPoint).Address))
            {
                _connection.BeginSend(response, 0, response.Length, SocketFlags.None, HandshakeSendCallback, null);
            }
            else
            {
                Console.WriteLine(@"Socks5 Auth failed");
                Close();
            }
        }

        private void HandshakeAuthSendCallback(IAsyncResult ar)
        {
            try
            {
                _connection.EndSend(ar);
                var bytesRead = _connection.Receive(_connetionRecvBuffer, 1024, 0); //_connection.EndReceive(ar);

                if (bytesRead >= 3)
                {
                    var user_len = _connetionRecvBuffer[1];
                    var pass_len = _connetionRecvBuffer[user_len + 2];
                    byte[] response = { 1, 0 };
                    var user = Encoding.UTF8.GetString(_connetionRecvBuffer, 2, user_len);
                    var pass = Encoding.UTF8.GetString(_connetionRecvBuffer, user_len + 3, pass_len);
                    if (AuthConnection(user, pass))
                    {
                        _connection.BeginSend(response, 0, response.Length, SocketFlags.None, HandshakeSendCallback, null);
                    }
                }
                else
                {
                    Console.WriteLine(@"failed to recv data in HandshakeAuthSendCallback");
                    Close();
                }
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                Close();
            }
        }

        private void HandshakeSendCallback(IAsyncResult ar)
        {
            try
            {
                _connection.EndSend(ar);

                // +-----+-----+-------+------+----------+----------+
                // | VER | CMD |  RSV  | ATYP | DST.ADDR | DST.PORT |
                // +-----+-----+-------+------+----------+----------+
                // |  1  |  1  | X'00' |  1   | Variable |    2     |
                // +-----+-----+-------+------+----------+----------+
                // Skip first 3 bytes, and read 2 more bytes to analysis the address.
                // 2 more bytes is designed if address is domain then we don't need to read once more to get the addr length.
                // TODO validate
                _connection.BeginReceive(_connetionRecvBuffer, 0, 3 + ADDR_ATYP_LEN + 1, SocketFlags.None, HandshakeReceive2Callback, null);
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                Close();
            }
        }

        private void HandshakeReceive2Callback(IAsyncResult ar)
        {
            try
            {
                var bytesRead = _connection.EndReceive(ar);
                if (bytesRead >= 5)
                {
                    command = _connetionRecvBuffer[1];
                    _remoteHeaderSendBuffer = new byte[bytesRead - 3];
                    Array.Copy(_connetionRecvBuffer, 3, _remoteHeaderSendBuffer, 0, _remoteHeaderSendBuffer.Length);

                    var size = 0;
                    switch (_remoteHeaderSendBuffer[0])
                    {
                        case ATYP_IPv4:
                            size = 4 - 1;
                            break;
                        case ATYP_IPv6:
                            size = 16 - 1;
                            break;
                        case ATYP_DOMAIN:
                            size = _remoteHeaderSendBuffer[1];
                            break;
                    }
                    if (size == 0)
                        throw new Exception("Wrong socks5 addr type");
                    HandshakeReceive3Callback(size + ADDR_PORT_LEN); // recv port
                }
                else
                {
                    Console.WriteLine(@"failed to recv data in HandshakeReceive2Callback");
                    Close();
                }
            }
            catch
            {
                // ignored
            }
        }

        private void HandshakeReceive3Callback(int bytesRemain)
        {
            try
            {
                var bytesRead = _connection.Receive(_connetionRecvBuffer, bytesRemain, 0);
                if (bytesRead > 0)
                {
                    Array.Resize(ref _remoteHeaderSendBuffer, _remoteHeaderSendBuffer.Length + bytesRead);
                    Array.Copy(_connetionRecvBuffer, 0, _remoteHeaderSendBuffer, _remoteHeaderSendBuffer.Length - bytesRead, bytesRead);
                    switch (command)
                    {
                        case CMD_UDP_ASSOC:
                            RspSocks5UDPHeader(bytesRead);
                            break;
                        case CMD_CONNECT:
                            local_sendback_protocol = @"socks5";
                            Connect();
                            break;
                        default:
                            Logging.Debug($@"Unsupported CMD={command}");
                            Close();
                            break;
                    }
                }
                else
                {
                    Console.WriteLine(@"failed to recv data in HandshakeReceive3Callback");
                    Close();
                }
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                Close();
            }
        }

        private void RspSocks5UDPHeader(int bytesRead)
        {
            var ipv6 = _connection.AddressFamily == AddressFamily.InterNetworkV6;
            var udpPort = 0;
            if (bytesRead >= 3 + 6)
            {
                ipv6 = _remoteHeaderSendBuffer[0] == 4;
                if (!ipv6)
                    udpPort = _remoteHeaderSendBuffer[5] * 0x100 + _remoteHeaderSendBuffer[6];
                else
                    udpPort = _remoteHeaderSendBuffer[17] * 0x100 + _remoteHeaderSendBuffer[18];
            }
            if (!ipv6)
            {
                _remoteHeaderSendBuffer = new byte[1 + 4 + 2];
                _remoteHeaderSendBuffer[0] = 0x8 | 1;
                _remoteHeaderSendBuffer[5] = (byte)(udpPort / 0x100);
                _remoteHeaderSendBuffer[6] = (byte)(udpPort % 0x100);
            }
            else
            {
                _remoteHeaderSendBuffer = new byte[1 + 16 + 2];
                _remoteHeaderSendBuffer[0] = 0x8 | 4;
                _remoteHeaderSendBuffer[17] = (byte)(udpPort / 0x100);
                _remoteHeaderSendBuffer[18] = (byte)(udpPort % 0x100);
            }

            var port = 0;
            var ip = ipv6 ? IPAddress.IPv6Any : IPAddress.Any;
            _connectionUDP = new Socket(ip.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            for (; port < 65536; ++port)
            {
                try
                {
                    _connectionUDP.Bind(new IPEndPoint(ip, port));
                    break;
                }
                catch (Exception)
                {
                    //
                }
            }
            port = ((IPEndPoint)_connectionUDP.LocalEndPoint).Port;
            if (!ipv6)
            {
                byte[] response = { 5, 0, 0, 1,
                                0, 0, 0, 0,
                                (byte)(port / 0x100), (byte)(port % 0x100) };
                var ip_bytes = ((IPEndPoint)_connection.LocalEndPoint).Address.GetAddressBytes();
                Array.Copy(ip_bytes, 0, response, 4, 4);
                _connection.Send(response);
                Connect();
            }
            else
            {
                byte[] response = { 5, 0, 0, 4,
                                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                (byte)(port / 0x100), (byte)(port % 0x100) };
                var ip_bytes = ((IPEndPoint)_connection.LocalEndPoint).Address.GetAddressBytes();
                Array.Copy(ip_bytes, 0, response, 4, 16);
                _connection.Send(response);
                Connect();
            }
        }

        private void RspHttpHandshakeReceive()
        {
            command = 1; // Set TCP connect command
            if (httpProxyState == null)
            {
                httpProxyState = new HttpParser();
            }
            if (IPSubnet.IsLoopBack(((IPEndPoint)_connection.RemoteEndPoint).Address))
            {
                httpProxyState.httpAuthUser = string.Empty;
                httpProxyState.httpAuthPass = string.Empty;
            }
            else
            {
                httpProxyState.httpAuthUser = _config.authUser;
                httpProxyState.httpAuthPass = _config.authPass;
            }
            for (var i = 1; ; ++i)
            {
                var err = httpProxyState.HandshakeReceive(_firstPacket, _firstPacketLength, out _remoteHeaderSendBuffer);
                if (err == 1)
                {
                    if (HttpHandshakeRecv())
                        break;
                }
                else if (err == 2)
                {
                    var dataSend = HttpParser.Http407();
                    var httpData = Encoding.UTF8.GetBytes(dataSend);
                    _connection.Send(httpData);
                    if (HttpHandshakeRecv())
                        break;
                }
                else if (err == 3 || err == 4)
                {
                    Connect();
                    break;
                }
                else if (err == 0)
                {
                    local_sendback_protocol = "http";
                    Connect();
                    break;
                }
                else if (err == 500)
                {
                    var dataSend = HttpParser.Http500();
                    var httpData = Encoding.UTF8.GetBytes(dataSend);
                    _connection.Send(httpData);
                    if (HttpHandshakeRecv())
                        break;
                }
                if (i == 3)
                {
                    Close();
                    break;
                }
            }
        }

        private bool HttpHandshakeRecv()
        {
            try
            {
                var bytesRead = _connection.Receive(_connetionRecvBuffer, _firstPacket.Length, 0);
                if (bytesRead > 0)
                {
                    Array.Copy(_connetionRecvBuffer, _firstPacket, bytesRead);
                    _firstPacketLength = bytesRead;
                    return false;
                }

                Console.WriteLine(@"failed to recv data in HttpHandshakeRecv");
                Close();
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                Close();
            }
            return true;
        }

        private void Connect()
        {
            Server GetCurrentServer(int localPort, ServerSelectStrategy.FilterFunc filter, string targetURI, bool cfgRandom, bool usingRandom, bool forceRandom)
            => _config.GetCurrentServer(localPort, filter, targetURI, cfgRandom, usingRandom, forceRandom);

            void KeepCurrentServer(int localPort, string targetURI, string id)
            {
                _config.KeepCurrentServer(localPort, targetURI, id);
            }

            var local_port = ((IPEndPoint)_connection.LocalEndPoint).Port;
            var handler = new Handler
            {
                getCurrentServer = GetCurrentServer,
                keepCurrentServer = KeepCurrentServer,
                connection = new ProxySocketTunLocal(_connection),
                connectionUDP = _connectionUDP,
                cfg =
                    {
                            ReconnectTimesRemain = _config.reconnectTimes,
                            Random = _config.random,
                            ForceRandom = _config.random
                    }
            };

            handler.setServerTransferTotal(_transfer);
            if (_config.proxyEnable)
            {
                handler.cfg.ProxyType = _config.proxyType;
                handler.cfg.Socks5RemoteHost = _config.proxyHost;
                handler.cfg.Socks5RemotePort = _config.proxyPort;
                handler.cfg.Socks5RemoteUsername = _config.proxyAuthUser;
                handler.cfg.Socks5RemotePassword = _config.proxyAuthPass;
                handler.cfg.ProxyUserAgent = _config.proxyUserAgent;
            }
            handler.cfg.Ttl = _config.TTL;
            handler.cfg.ConnectTimeout = _config.connectTimeout;
            handler.cfg.AutoSwitchOff = _config.autoBan;
            if (!string.IsNullOrEmpty(_config.localDnsServer))
            {
                handler.cfg.LocalDnsServers = _config.localDnsServer;
            }
            if (!string.IsNullOrEmpty(_config.dnsServer))
            {
                handler.cfg.DnsServers = _config.dnsServer;
            }
            if (_config.GetPortMapCache().ContainsKey(local_port))
            {
                var cfg = _config.GetPortMapCache()[local_port];
                if (cfg.server == null || cfg.id == cfg.server.Id)
                {
                    if (cfg.server != null)
                    {
                        handler.select_server = (server, selServer) => server.Id == cfg.server.Id;
                    }
                    else if (!string.IsNullOrEmpty(cfg.id))
                    {
                        handler.select_server = (server, selServer) => server.Group == cfg.id;
                    }
                    if (cfg.type == PortMapType.Forward) // tunnel
                    {
                        var addr = Encoding.UTF8.GetBytes(cfg.server_addr);
                        var newFirstPacket = new byte[_firstPacketLength + addr.Length + 4];
                        newFirstPacket[0] = 3;
                        newFirstPacket[1] = (byte)addr.Length;
                        Array.Copy(addr, 0, newFirstPacket, 2, addr.Length);
                        newFirstPacket[addr.Length + 2] = (byte)(cfg.server_port / 256);
                        newFirstPacket[addr.Length + 3] = (byte)(cfg.server_port % 256);
                        Array.Copy(_firstPacket, 0, newFirstPacket, addr.Length + 4, _firstPacketLength);
                        _remoteHeaderSendBuffer = newFirstPacket;
                        handler.Start(_remoteHeaderSendBuffer, _remoteHeaderSendBuffer.Length, null);
                    }
                    else if (_connectionUDP == null && cfg.type == PortMapType.RuleProxy
                        && new Socks5Forwarder(_config, _IPRange).Handle(_remoteHeaderSendBuffer, _remoteHeaderSendBuffer.Length, _connection, local_sendback_protocol))
                    {
                    }
                    else
                    {
                        handler.Start(_remoteHeaderSendBuffer, _remoteHeaderSendBuffer.Length, @"socks5");
                    }
                    Dispose();
                    return;
                }
            }
            else
            {
                if (_connectionUDP == null && new Socks5Forwarder(_config, _IPRange).Handle(_remoteHeaderSendBuffer, _remoteHeaderSendBuffer.Length, _connection, local_sendback_protocol))
                {
                }
                else
                {
                    handler.Start(_remoteHeaderSendBuffer, _remoteHeaderSendBuffer.Length, local_sendback_protocol);
                }
                Dispose();
                return;
            }
            Dispose();
            Close();
        }

        private void Dispose()
        {
            _transfer = null;
            _IPRange = null;

            _firstPacket = null;
            _connection = null;
            _connectionUDP = null;

            _connetionRecvBuffer = null;
            _remoteHeaderSendBuffer = null;

            httpProxyState = null;
        }
    }
}
