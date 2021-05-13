using System;

namespace Shadowsocks.Controller.Service
{
    internal class ProtocolResponseDetector
    {
        protected enum Protocol
        {
            Unknown = -1,
            NotBegin = 0,
            HTTP = 1,
            TLS = 2,
            SOCKS4 = 4,
            SOCKS5 = 5
        }

        protected Protocol protocol = Protocol.NotBegin;
        private byte[] _sendBuffer = new byte[0];
        private byte[] _recvBuffer = new byte[0];

        public bool Pass { get; set; }

        public ProtocolResponseDetector()
        {
            Pass = false;
        }

        public void OnSend(byte[] sendData, int length)
        {
            if (protocol != Protocol.NotBegin)
            {
                return;
            }

            Array.Resize(ref _sendBuffer, _sendBuffer.Length + length);
            Array.Copy(sendData, 0, _sendBuffer, _sendBuffer.Length - length, length);

            if (_sendBuffer.Length < 2)
            {
                return;
            }

            var head_size = Obfs.ObfsBase.GetHeadSize(_sendBuffer, _sendBuffer.Length);
            if (_sendBuffer.Length - head_size < 0)
            {
                return;
            }

            var data = new byte[_sendBuffer.Length - head_size];
            Array.Copy(_sendBuffer, head_size, data, 0, data.Length);

            if (data.Length < 2)
            {
                return;
            }

            if (data.Length > 8)
            {
                if (data[0] == 22 && data[1] == 3 && data[2] <= 3)
                {
                    protocol = Protocol.TLS;
                    return;
                }
                if (data[0] == 'G' && data[1] == 'E' && data[2] == 'T' && data[3] == ' '
                    || data[0] == 'P' && data[1] == 'U' && data[2] == 'T' && data[3] == ' '
                    || data[0] == 'H' && data[1] == 'E' && data[2] == 'A' && data[3] == 'D' && data[4] == ' '
                    || data[0] == 'P' && data[1] == 'O' && data[2] == 'S' && data[3] == 'T' && data[4] == ' '
                    || data[0] == 'C' && data[1] == 'O' && data[2] == 'N' && data[3] == 'N' && data[4] == 'E' && data[5] == 'C' && data[6] == 'T' && data[7] == ' '
                )
                {
                    protocol = Protocol.HTTP;
                }
            }
            else
            {
                protocol = Protocol.Unknown;
            }
        }

        public int OnRecv(byte[] recv_data, int length)
        {
            if (protocol is Protocol.Unknown or Protocol.NotBegin)
            {
                return 0;
            }

            Array.Resize(ref _recvBuffer, _recvBuffer.Length + length);
            Array.Copy(recv_data, 0, _recvBuffer, _recvBuffer.Length - length, length);

            if (_recvBuffer.Length < 2)
            {
                return 0;
            }

            if (protocol == Protocol.HTTP && _recvBuffer.Length > 4)
            {
                if (_recvBuffer[0] == 'H' && _recvBuffer[1] == 'T' && _recvBuffer[2] == 'T' && _recvBuffer[3] == 'P')
                {
                    Finish();
                    return 0;
                }

                protocol = Protocol.Unknown;
                return 1;
                //throw new ProtocolException("Wrong http response");
            }

            if (protocol == Protocol.TLS && _recvBuffer.Length > 4)
            {
                if (_recvBuffer[0] == 22 && _recvBuffer[1] == 3)
                {
                    Finish();
                    return 0;
                }

                protocol = Protocol.Unknown;
                return 2;
                //throw new ProtocolException("Wrong tls response");
            }
            return 0;
        }

        private void Finish()
        {
            _sendBuffer = null;
            _recvBuffer = null;
            protocol = Protocol.Unknown;
            Pass = true;
        }
    }
}
