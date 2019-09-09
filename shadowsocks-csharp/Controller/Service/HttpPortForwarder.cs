using Shadowsocks.Model;
using Shadowsocks.Proxy;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Shadowsocks.Controller.Service
{
    class HttpPortForwarder : Listener.Service
    {
        int _targetPort;
        Configuration _config;

        public HttpPortForwarder(int targetPort, Configuration config)
        {
            _targetPort = targetPort;
            _config = config;
        }

        public bool Handle(byte[] firstPacket, int length, Socket socket)
        {
            new Handler().Start(_config, firstPacket, length, socket, _targetPort);
            return true;
        }

        class Handler
        {
            private byte[] _firstPacket;
            private int _firstPacketLength;
            private int _targetPort;
            private Socket _local;
            private Socket _remote;
            private bool _closed;
            private Configuration _config;
            HttpParser httpProxyState;
            public const int RecvSize = 4096;
            // remote receive buffer
            private byte[] remoteRecvBuffer = new byte[RecvSize];
            // connection receive buffer
            private byte[] connetionRecvBuffer = new byte[RecvSize];

            public void Start(Configuration config, byte[] firstPacket, int length, Socket socket, int targetPort)
            {
                _firstPacket = firstPacket;
                _firstPacketLength = length;
                _local = socket;
                _targetPort = targetPort;
                _config = config;
                if ((_config.authUser ?? "").Length == 0 || Util.Utils.isMatchSubNet(((IPEndPoint)_local.RemoteEndPoint).Address, "127.0.0.0/8"))
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
                if (httpProxyState == null)
                {
                    httpProxyState = new HttpParser(true);
                }
                httpProxyState.httpAuthUser = _config.authUser;
                httpProxyState.httpAuthPass = _config.authPass;
                var err = httpProxyState.HandshakeReceive(_firstPacket, _firstPacketLength, out _);
                if (err == 1)
                {
                    _local.BeginReceive(connetionRecvBuffer, 0, _firstPacket.Length, 0,
                        HttpHandshakeRecv, null);
                }
                else if (err == 2)
                {
                    var dataSend = httpProxyState.Http407();
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
                    var dataSend = httpProxyState.Http200();
                    var httpData = System.Text.Encoding.UTF8.GetBytes(dataSend);
                    _local.BeginSend(httpData, 0, httpData.Length, 0, StartConnect, null);
                }
                else if (err == 500)
                {
                    var dataSend = httpProxyState.Http500();
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
                        Array.Copy(connetionRecvBuffer, _firstPacket, bytesRead);
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
                    _local.BeginReceive(connetionRecvBuffer, 0, _firstPacket.Length, 0,
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
                    var ipAddress = IPAddress.Loopback;
                    var remoteEP = new IPEndPoint(ipAddress, _targetPort);

                    _remote = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };

                    // Connect to the remote endpoint.
                    _remote.BeginConnect(remoteEP,
                        ConnectCallback, null);
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
                    _remote.BeginSend(_firstPacket, 0, _firstPacketLength, 0, StartPipe, null);
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
                    _remote.BeginReceive(remoteRecvBuffer, 0, RecvSize, 0,
                        PipeRemoteReceiveCallback, null);
                    _local.BeginReceive(connetionRecvBuffer, 0, RecvSize, 0,
                        PipeConnectionReceiveCallback, null);
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
                        _local.BeginSend(remoteRecvBuffer, 0, bytesRead, 0, PipeConnectionSendCallback, null);
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
                        _remote.BeginSend(connetionRecvBuffer, 0, bytesRead, 0, PipeRemoteSendCallback, null);
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

            private void PipeRemoteSendCallback(IAsyncResult ar)
            {
                if (_closed)
                {
                    return;
                }
                try
                {
                    _remote.EndSend(ar);
                    _local.BeginReceive(connetionRecvBuffer, 0, RecvSize, 0,
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
                    _remote.BeginReceive(remoteRecvBuffer, 0, RecvSize, 0,
                        PipeRemoteReceiveCallback, null);
                }
                catch (Exception e)
                {
                    Logging.LogUsefulException(e);
                    Close();
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
                Thread.Sleep(100);
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
                        _remote.Close();
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
