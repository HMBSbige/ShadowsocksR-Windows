using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Shadowsocks.Proxy
{
    public class ProxySocketTun
    {
        protected Socket _socket;
        protected EndPoint _socketEndPoint;
        protected IPEndPoint _remoteUDPEndPoint;

        protected bool _proxy;
        protected string _proxy_server;
        protected int _proxy_udp_port;

        protected const int RecvSize = 1460 * 2;

        protected bool _close;

        public ProxySocketTun(Socket socket)
        {
            _socket = socket;
        }

        public ProxySocketTun(AddressFamily af, SocketType type, ProtocolType protocol)
        {
            _socket = new Socket(af, type, protocol);
        }

        public Socket GetSocket()
        {
            return _socket;
        }

        public bool IsClose => _close;

        public bool GoS5Proxy
        {
            get => _proxy;
            set => _proxy = value;
        }

        public AddressFamily AddressFamily => _socket.AddressFamily;

        public int Available => _socket.Available;

        public void Shutdown(SocketShutdown how)
        {
            _socket.Shutdown(how);
        }

        public void Close()
        {
            _socket.Close();
            _socket = null;
        }

        public IAsyncResult BeginConnect(EndPoint ep, AsyncCallback callback, object state)
        {
            _close = false;
            _socketEndPoint = ep;
            return _socket.BeginConnect(ep, callback, state);
        }

        public void EndConnect(IAsyncResult ar)
        {
            _socket.EndConnect(ar);
        }

        public int Receive(byte[] buffer, int size, SocketFlags flags)
        {
            return _socket.Receive(buffer, size, flags);
        }

        public IAsyncResult BeginReceive(byte[] buffer, int size, SocketFlags flags, AsyncCallback callback, object state)
        {
            var st = new CallbackState();
            st.buffer = buffer;
            st.size = size;
            st.state = state;
            return _socket.BeginReceive(buffer, 0, size, flags, callback, st);
        }

        public int EndReceive(IAsyncResult ar)
        {
            var bytesRead = _socket.EndReceive(ar);
            if (bytesRead > 0)
            {
                var st = (CallbackState)ar.AsyncState;
                st.size = bytesRead;
                return bytesRead;
            }

            _close = true;
            return bytesRead;
        }

        public int SendAll(byte[] buffer, int size, SocketFlags flags)
        {
            var sendSize = _socket.Send(buffer, size, flags);
            while (sendSize < size)
            {
                var new_size = _socket.Send(buffer, sendSize, size - sendSize, flags);
                sendSize += new_size;
            }
            return size;
        }

        public virtual int Send(byte[] buffer, int size, SocketFlags flags)
        {
            return SendAll(buffer, size, flags);
        }

        public int BeginSend(byte[] buffer, int size, SocketFlags flags, AsyncCallback callback, object state)
        {
            var st = new CallbackState();
            st.size = size;
            st.state = state;

            _socket.BeginSend(buffer, 0, size, flags, callback, st);
            return size;
        }

        public int EndSend(IAsyncResult ar)
        {
            return _socket.EndSend(ar);
        }

        public IAsyncResult BeginReceiveFrom(byte[] buffer, int size, SocketFlags flags, ref EndPoint ep, AsyncCallback callback, object state)
        {
            var st = new CallbackState();
            st.buffer = buffer;
            st.size = size;
            st.state = state;
            return _socket.BeginReceiveFrom(buffer, 0, size, flags, ref ep, callback, st);
        }

        public int GetAsyncResultSize(IAsyncResult ar)
        {
            var st = (CallbackState)ar.AsyncState;
            return st.size;
        }

        public byte[] GetAsyncResultBuffer(IAsyncResult ar)
        {
            var st = (CallbackState)ar.AsyncState;
            return st.buffer;
        }

        public bool ConnectSocks5ProxyServer(string strRemoteHost, int iRemotePort, bool udp, string socks5RemoteUsername, string socks5RemotePassword)
        {
            var socketErrorCode = (int)SocketError.ConnectionReset;
            _proxy = true;

            //构造Socks5代理服务器第一连接头(无用户名密码)
            var bySock5Send = new byte[10];
            bySock5Send[0] = 5;
            bySock5Send[1] = socks5RemoteUsername.Length == 0 ? (byte)1 : (byte)2;
            bySock5Send[2] = 0;
            bySock5Send[3] = 2;

            //发送Socks5代理第一次连接信息
            _socket.Send(bySock5Send, bySock5Send[1] + 2, SocketFlags.None);

            var bySock5Receive = new byte[32];
            var iRecCount = _socket.Receive(bySock5Receive, bySock5Receive.Length, SocketFlags.None);

            if (iRecCount < 2)
            {
                throw new SocketException(socketErrorCode);
            }

            if (bySock5Receive[0] != 5 || bySock5Receive[1] != 0 && bySock5Receive[1] != 2)
            {
                throw new SocketException(socketErrorCode);
            }

            if (bySock5Receive[1] != 0) // auth
            {
                if (bySock5Receive[1] == 2)
                {
                    if (socks5RemoteUsername.Length == 0)
                    {
                        throw new SocketException(socketErrorCode);
                    }

                    bySock5Send = new byte[socks5RemoteUsername.Length + socks5RemotePassword.Length + 3];
                    bySock5Send[0] = 1;
                    bySock5Send[1] = (byte)socks5RemoteUsername.Length;
                    for (var i = 0; i < socks5RemoteUsername.Length; ++i)
                    {
                        bySock5Send[2 + i] = (byte)socks5RemoteUsername[i];
                    }
                    bySock5Send[socks5RemoteUsername.Length + 2] = (byte)socks5RemotePassword.Length;
                    for (var i = 0; i < socks5RemotePassword.Length; ++i)
                    {
                        bySock5Send[socks5RemoteUsername.Length + 3 + i] = (byte)socks5RemotePassword[i];
                    }
                    _socket.Send(bySock5Send, bySock5Send.Length, SocketFlags.None);
                    _socket.Receive(bySock5Receive, bySock5Receive.Length, SocketFlags.None);

                    if (bySock5Receive[0] != 1 || bySock5Receive[1] != 0)
                    {
                        throw new SocketException((int)SocketError.ConnectionRefused);
                    }
                }
                else
                {
                    return false;
                }
            }
            // connect
            if (!udp) // TCP
            {
                var dataSock5Send = new List<byte> { 5, 1, 0 };

                if (!IPAddress.TryParse(strRemoteHost, out var ipAdd))
                {
                    dataSock5Send.Add(3); // remote DNS resolve
                    dataSock5Send.Add((byte)strRemoteHost.Length);
                    dataSock5Send.AddRange(strRemoteHost.Select(t => (byte)t));
                }
                else
                {
                    var addBytes = ipAdd.GetAddressBytes();
                    if (addBytes.GetLength(0) > 4)
                    {
                        dataSock5Send.Add(4); // IPv6
                        for (var i = 0; i < 16; ++i)
                        {
                            dataSock5Send.Add(addBytes[i]);
                        }
                    }
                    else
                    {
                        dataSock5Send.Add(1); // IPv4
                        for (var i = 0; i < 4; ++i)
                        {
                            dataSock5Send.Add(addBytes[i]);
                        }
                    }
                }

                dataSock5Send.Add((byte)(iRemotePort / 256));
                dataSock5Send.Add((byte)(iRemotePort % 256));

                _socket.Send(dataSock5Send.ToArray(), dataSock5Send.Count, SocketFlags.None);
                iRecCount = _socket.Receive(bySock5Receive, bySock5Receive.Length, SocketFlags.None);

                if (iRecCount < 2 || bySock5Receive[0] != 5 || bySock5Receive[1] != 0)
                {
                    throw new SocketException(socketErrorCode);
                    //throw new Exception("第二次连接Socks5代理返回数据出错。");
                }
                return true;
            }
            else // UDP
            {
                var dataSock5Send = new List<byte>();
                dataSock5Send.Add(5);
                dataSock5Send.Add(3);
                dataSock5Send.Add(0);

                var ipAdd = ((IPEndPoint)_socketEndPoint).Address;
                {
                    var addBytes = ipAdd.GetAddressBytes();
                    if (addBytes.GetLength(0) > 4)
                    {
                        dataSock5Send.Add(4); // IPv6
                        for (var i = 0; i < 16; ++i)
                        {
                            dataSock5Send.Add(addBytes[i]);
                        }
                    }
                    else
                    {
                        dataSock5Send.Add(1); // IPv4
                        for (var i = 0; i < 4; ++i)
                        {
                            dataSock5Send.Add(addBytes[i]);
                        }
                    }
                }

                dataSock5Send.Add(0);
                dataSock5Send.Add(0);

                _socket.Send(dataSock5Send.ToArray(), dataSock5Send.Count, SocketFlags.None);
                _socket.Receive(bySock5Receive, bySock5Receive.Length, SocketFlags.None);

                if (bySock5Receive[0] != 5 || bySock5Receive[1] != 0)
                {
                    throw new SocketException(socketErrorCode);
                    //throw new Exception("第二次连接Socks5代理返回数据出错。");
                }

                var ipv6 = bySock5Receive[0] == 4;
                byte[] addr;
                int port;
                if (!ipv6)
                {
                    addr = new byte[4];
                    Array.Copy(bySock5Receive, 4, addr, 0, 4);
                    port = bySock5Receive[8] * 0x100 + bySock5Receive[9];
                }
                else
                {
                    addr = new byte[16];
                    Array.Copy(bySock5Receive, 4, addr, 0, 16);
                    port = bySock5Receive[20] * 0x100 + bySock5Receive[21];
                }
                ipAdd = new IPAddress(addr);
                _remoteUDPEndPoint = new IPEndPoint(ipAdd, port);
                return true;
            }
        }

        public void SetTcpServer(string server, int port)
        {
            _proxy_server = server;
            _proxy_udp_port = port;
        }

        public void SetUdpServer(string server, int port)
        {
            _proxy_server = server;
            _proxy_udp_port = port;
        }

        public void SetUdpEndPoint(IPEndPoint ep)
        {
            _remoteUDPEndPoint = ep;
        }

        public bool ConnectHttpProxyServer(string strRemoteHost, int iRemotePort, string socks5RemoteUsername, string socks5RemotePassword, string proxyUserAgent)
        {
            _proxy = true;

            if (IPAddress.TryParse(strRemoteHost, out var ipAdd))
            {
                strRemoteHost = ipAdd.ToString();
            }
            var host = (strRemoteHost.IndexOf(':') >= 0 ? "[" + strRemoteHost + "]" : strRemoteHost) + ":" + iRemotePort;
            var authstr = Convert.ToBase64String(Encoding.UTF8.GetBytes(socks5RemoteUsername + ":" + socks5RemotePassword));
            var cmd = "CONNECT " + host + " HTTP/1.0\r\n"
                + "Host: " + host + "\r\n";
            if (!string.IsNullOrEmpty(proxyUserAgent))
                cmd += "User-Agent: " + proxyUserAgent + "\r\n";
            cmd += "Proxy-Connection: Keep-Alive\r\n";
            if (socks5RemoteUsername.Length > 0)
                cmd += "Proxy-Authorization: Basic " + authstr + "\r\n";
            cmd += "\r\n";
            var httpData = Encoding.UTF8.GetBytes(cmd);
            _socket.Send(httpData, httpData.Length, SocketFlags.None);
            var byReceive = new byte[1024];
            var iRecCount = _socket.Receive(byReceive, byReceive.Length, SocketFlags.None);
            if (iRecCount > 13)
            {
                var data = Encoding.UTF8.GetString(byReceive, 0, iRecCount);
                var data_part = data.Split(' ');
                if (data_part.Length > 1 && data_part[1] == "200")
                {
                    return true;
                }
            }
            return false;
        }
    }
}
