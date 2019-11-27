using Shadowsocks.Model;
using Shadowsocks.Model.Transfer;
using Shadowsocks.Proxy;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Shadowsocks.Controller.Service
{
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

        private static bool Accept(byte[] firstPacket, int length)
        {
            if (length < 2)
            {
                return false;
            }
            if (firstPacket[0] == 5 || firstPacket[0] == 4)
            {
                return true;
            }
            Debug.WriteLine(System.Text.Encoding.UTF8.GetString(firstPacket));
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

        public override bool Handle(byte[] firstPacket, int length, Socket socket)
        {
            if (!_config.PortMapCache.ContainsKey(((IPEndPoint)socket.LocalEndPoint).Port) && !Accept(firstPacket, length))
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
}
