using System.Net.Sockets;

namespace Shadowsocks.Proxy
{
    public class ProxySocketTunLocal : ProxySocketTun
    {
        public string local_sendback_protocol;

        public ProxySocketTunLocal(Socket socket) : base(socket)
        {
        }

        public override int Send(byte[] buffer, int size, SocketFlags flags)
        {
            if (local_sendback_protocol != null)
            {
                if (local_sendback_protocol == "http")
                {
                    var data = System.Text.Encoding.UTF8.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                    _socket.Send(data, data.Length, 0);
                }
                else if (local_sendback_protocol == "socks5")
                {
                    if (_socket.AddressFamily == AddressFamily.InterNetwork)
                    {
                        byte[] response =
                        {
                                5, 0, 0, 1,
                                0, 0, 0, 0,
                                0, 0
                        };
                        _socket.Send(response);
                    }
                    else
                    {
                        byte[] response =
                        {
                                5, 0, 0, 4,
                                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                0, 0
                        };
                        _socket.Send(response);
                    }
                }

                local_sendback_protocol = null;
            }

            return SendAll(buffer, size, flags);
        }

    }
}
