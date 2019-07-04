using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Shadowsocks.Util
{
    public class ConnectionException : ApplicationException
    {
        public ConnectionException(string message) : base(message)
        { }
    }
    public static class SimpleSocksProxy
    {
        #region ErrorMessages
        private static readonly string[] ErrorMessages =  {
                                        @"Operation completed successfully.",
                                        @"General SOCKS server failure.",
                                        @"connection not allowed by ruleset.",
                                        @"Network unreachable.",
                                        @"Host unreachable.",
                                        @"Connection refused.",
                                        @"TTL expired.",
                                        @"Command not supported.",
                                        @"Address type not supported.",
                                        @"Unknown error."
                                    };
        #endregion

        private static Socket ConnectToSocks5Proxy(string proxyAddress, ushort proxyPort,
        string destAddress, ushort destPort,
        string userName, string password)
        {
            if (userName == null)
            {
                userName = string.Empty;
            }
            if (password == null)
            {
                password = string.Empty;
            }

            var request = new byte[257];
            var response = new byte[257];

            if (IPAddress.TryParse(proxyAddress, out var proxyIp))
            {
                proxyIp = Dns.GetHostAddresses(proxyAddress)[0];
            }

            IPAddress.TryParse(destAddress, out var destIp);

            var proxyEndPoint = new IPEndPoint(proxyIp, proxyPort);

            // open a TCP connection to SOCKS server...
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s.Connect(proxyEndPoint);

            ushort nIndex = 0;
            request[nIndex++] = 0x05; // Version 5.
            request[nIndex++] = 0x02; // 2 Authentication methods are in packet...
            request[nIndex++] = 0x00; // NO AUTHENTICATION REQUIRED
            request[nIndex++] = 0x02; // USERNAME/PASSWORD
                                      // Send the authentication negotiation request...
            s.Send(request, nIndex, SocketFlags.None);

            // Receive 2 byte response...
            var nGot = s.Receive(response, 2, SocketFlags.None);
            if (nGot != 2)
                throw new ConnectionException("Bad response received from proxy server.");

            if (response[1] == 0xFF)
            {   // No authentication method was accepted close the socket.
                s.Close();
                throw new ConnectionException("None of the authentication method was accepted by proxy server.");
            }

            byte[] rawBytes;

            if (/*response[1]==0x02*/true)
            {//Username/Password Authentication protocol
                nIndex = 0;
                request[nIndex++] = 0x05; // Version 5.

                // add user name
                request[nIndex++] = (byte)userName.Length;
                rawBytes = Encoding.Default.GetBytes(userName);
                rawBytes.CopyTo(request, nIndex);
                nIndex += (ushort)rawBytes.Length;

                // add password
                request[nIndex++] = (byte)password.Length;
                rawBytes = Encoding.Default.GetBytes(password);
                rawBytes.CopyTo(request, nIndex);
                nIndex += (ushort)rawBytes.Length;

                // Send the Username/Password request
                s.Send(request, nIndex, SocketFlags.None);
                // Receive 2 byte response...
                nGot = s.Receive(response, 2, SocketFlags.None);
                if (nGot != 2)
                    throw new ConnectionException("Bad response received from proxy server.");
                if (response[1] != 0x00)
                    throw new ConnectionException("Bad Username/Password.");
            }
            // This version only supports connect command. 
            // UDP and Bind are not supported.

            // Send connect request now...
            nIndex = 0;
            request[nIndex++] = 0x05;   // version 5.
            request[nIndex++] = 0x01;   // command = connect.
            request[nIndex++] = 0x00;   // Reserve = must be 0x00

            if (destIp != null)
            {
                if (destIp.AddressFamily == AddressFamily.InterNetwork)
                {
                    // Address is IPV4 format
                    request[nIndex++] = 0x01;
                    rawBytes = destIp.GetAddressBytes();
                    rawBytes.CopyTo(request, nIndex);
                    nIndex += (ushort)rawBytes.Length;
                }
                else if (destIp.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    // Address is IPV6 format
                    request[nIndex++] = 0x04;
                    rawBytes = destIp.GetAddressBytes();
                    rawBytes.CopyTo(request, nIndex);
                    nIndex += (ushort)rawBytes.Length;
                }
            }
            else
            {
                // Address is full-qualified domain name.
                request[nIndex++] = 0x03;
                // length of address.
                request[nIndex++] = Convert.ToByte(destAddress.Length);
                rawBytes = Encoding.Default.GetBytes(destAddress);
                rawBytes.CopyTo(request, nIndex);
                nIndex += (ushort)rawBytes.Length;
            }

            // using big-endian byte order
            var portBytes = BitConverter.GetBytes(destPort);
            Array.Reverse(portBytes);
            foreach (var b in portBytes)
            {
                request[nIndex++] = b;
            }

            // send connect request.
            s.Send(request, nIndex, SocketFlags.None);
            // Get variable length response...
            s.ReceiveTimeout = 3000;
            s.Receive(response);
            if (response[1] != 0x00)
            {
                throw new ConnectionException(ErrorMessages[response[1]]);
            }
            // Success Connected...
            return s;
        }

        public static bool TestLocalSocks5(ushort port, string pass, string user)
        {
            const string strGet = "GET / HTTP/1.1\n\r\n";
            var bytesReceived = new byte[1024];
            Socket client = null;
            try
            {
                client = ConnectToSocks5Proxy(
                        IPAddress.Loopback.ToString(), port,
                        @"www.google.com",
                        80, pass, user);
                client.Send(Encoding.UTF8.GetBytes(strGet));
                var bytes = client.Receive(bytesReceived, bytesReceived.Length, 0);
                var page = Encoding.UTF8.GetString(bytesReceived, 0, bytes);
                if (page.StartsWith(@"HTTP/1.1 200 OK"))
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                client?.Close();
                client?.Dispose();
            }
        }
    }
}
