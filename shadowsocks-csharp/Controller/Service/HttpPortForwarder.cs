using Shadowsocks.Model;
using Shadowsocks.Proxy;
using Shadowsocks.Util.NetUtils;
using System;
using System.Net;
using System.Net.Sockets;

namespace Shadowsocks.Controller.Service
{
    internal class HttpPortForwarder : Listener.Service
    {
        private readonly int _targetPort;
        private readonly Configuration _config;

        public HttpPortForwarder(int targetPort, Configuration config)
        {
            _targetPort = targetPort;
            _config = config;
        }

        public override bool Handle(byte[] firstPacket, int length, Socket socket)
        {
            if (socket.ProtocolType != ProtocolType.Tcp)
            {
                return false;
            }
            new Handler().Start(_config, firstPacket, length, socket, _targetPort);
            return true;
        }

        private class Handler
        {
            private byte[] _firstPacket;
            private int _firstPacketLength;
            private int _targetPort;
            private Socket _local;
            private WrappedSocket _remote;
            private bool _closed;
            private bool _localShutdown;
            private bool _remoteShutdown;
            private Configuration _config;

            private HttpParser _httpProxyState;

            private const int RecvSize = 4096;
            // remote receive buffer
            private readonly byte[] _remoteRecvBuffer = new byte[RecvSize];
            // connection receive buffer
            private readonly byte[] _connetionRecvBuffer = new byte[RecvSize];

            public void Start(Configuration config, byte[] firstPacket, int length, Socket socket, int targetPort)
            {
                _config = config;
                _firstPacket = firstPacket;
                _firstPacketLength = length;
                _local = socket;
                _targetPort = targetPort;
                if ((_config.AuthUser ?? string.Empty).Length == 0
                || IPSubnet.IsLoopBack(((IPEndPoint)_local.RemoteEndPoint).Address))
                {
                    Connect();
                }
                else
                {
                    RspHttpHandshakeReceive();
                }
            }

            private void RspHttpHandshakeReceive()
            {
                if (_httpProxyState == null)
                {
                    _httpProxyState = new HttpParser(true);
                }
                _httpProxyState.httpAuthUser = _config.AuthUser;
                _httpProxyState.httpAuthPass = _config.AuthPass;
                var err = _httpProxyState.HandshakeReceive(_firstPacket, _firstPacketLength, out _);
                if (err == 1)
                {
                    _local.BeginReceive(_connetionRecvBuffer, 0, _firstPacket.Length, 0,
                        HttpHandshakeRecv, null);
                }
                else if (err == 2)
                {
                    var dataSend = HttpParser.Http407();
                    var httpData = System.Text.Encoding.UTF8.GetBytes(dataSend);
                    _local.BeginSend(httpData, 0, httpData.Length, 0, HttpHandshakeAuthEndSend, null);
                }
                else if (err == 3)
                {
                    Connect();
                }
                else if (err == 4)
                {
                    Connect();
                }
                else if (err == 0)
                {
                    var dataSend = HttpParser.Http200();
                    var httpData = System.Text.Encoding.UTF8.GetBytes(dataSend);
                    _local.BeginSend(httpData, 0, httpData.Length, 0, StartConnect, null);
                }
                else if (err == 500)
                {
                    var dataSend = HttpParser.Http500();
                    var httpData = System.Text.Encoding.UTF8.GetBytes(dataSend);
                    _local.BeginSend(httpData, 0, httpData.Length, 0, HttpHandshakeAuthEndSend, null);
                }
            }

            private void HttpHandshakeRecv(IAsyncResult ar)
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
                        Array.Copy(_connetionRecvBuffer, _firstPacket, bytesRead);
                        _firstPacketLength = bytesRead;
                        RspHttpHandshakeReceive();
                    }
                    else
                    {
                        Console.WriteLine(@"failed to recv data in HttpHandshakeRecv");
                        Close();
                    }
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
                }
            }

            private void HttpHandshakeAuthEndSend(IAsyncResult ar)
            {
                if (_closed)
                {
                    return;
                }
                try
                {
                    _local.EndSend(ar);
                    _local.BeginReceive(_connetionRecvBuffer, 0, _firstPacket.Length, 0,
                        HttpHandshakeRecv, null);
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
                }
            }

            private void StartConnect(IAsyncResult ar)
            {
                try
                {
                    _local.EndSend(ar);
                    Connect();
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
                }

            }

            private void Connect()
            {
                try
                {
                    var ipAddress = Global.OSSupportsLocalIPv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback;
                    var remoteEp = new IPEndPoint(ipAddress, _targetPort);

                    _remote = new WrappedSocket();

                    // Connect to the remote endpoint.
                    _remote.BeginConnect(remoteEp, ConnectCallback, null);
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
                }
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
                    _remote.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
                    HandshakeReceive();
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
                }
            }

            private void HandshakeReceive()
            {
                if (_closed)
                {
                    return;
                }
                try
                {
                    _remote.BeginSend(_firstPacket, 0, _firstPacketLength, SocketFlags.None, StartPipe, null);
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
                }
            }

            private void StartPipe(IAsyncResult ar)
            {
                if (_closed)
                {
                    return;
                }
                try
                {
                    _remote.EndSend(ar);
                    _remote.BeginReceive(_remoteRecvBuffer, 0, RecvSize, SocketFlags.None, PipeRemoteReceiveCallback, null);
                    _local.BeginReceive(_connetionRecvBuffer, 0, RecvSize, SocketFlags.None, PipeConnectionReceiveCallback, null);
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
                        _local.BeginSend(_remoteRecvBuffer, 0, bytesRead, 0, PipeConnectionSendCallback, null);
                    }
                    else
                    {
                        _local.Shutdown(SocketShutdown.Send);
                        _localShutdown = true;
                        CheckClose();
                    }
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
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
                        _remote.BeginSend(_connetionRecvBuffer, 0, bytesRead, SocketFlags.None, PipeRemoteSendCallback, null);
                    }
                    else
                    {
                        _remote.Shutdown(SocketShutdown.Send);
                        _remoteShutdown = true;
                        CheckClose();
                    }
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
                }
            }

            private void PipeRemoteSendCallback(IAsyncResult ar)
            {
                if (_closed)
                {
                    return;
                }
                try
                {
                    _remote.EndSend(ar);
                    _local.BeginReceive(_connetionRecvBuffer, 0, RecvSize, 0,
                        PipeConnectionReceiveCallback, null);
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
                }
            }

            private void PipeConnectionSendCallback(IAsyncResult ar)
            {
                if (_closed)
                {
                    return;
                }
                try
                {
                    _local.EndSend(ar);
                    _remote.BeginReceive(_remoteRecvBuffer, 0, RecvSize, 0,
                        PipeRemoteReceiveCallback, null);
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
                }
            }

            private void CheckClose()
            {
                if (_localShutdown && _remoteShutdown)
                {
                    Close();
                }
            }

            private void Close()
            {
                lock (this)
                {
                    if (_closed)
                    {
                        return;
                    }
                    _closed = true;
                }
                if (_local != null)
                {
                    try
                    {
                        _local.Shutdown(SocketShutdown.Both);
                        _local.Close();
                    }
                    catch (Exception e)
                    {
                        Logging.LogUsefulException(e);
                    }
                }
                if (_remote != null)
                {
                    try
                    {
                        _remote.Shutdown(SocketShutdown.Both);
                        _remote.Dispose();
                    }
                    catch (SocketException e)
                    {
                        Logging.LogUsefulException(e);
                    }
                }
            }
        }
    }
}
