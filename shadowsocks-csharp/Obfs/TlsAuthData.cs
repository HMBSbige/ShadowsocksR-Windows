using System.Collections.Generic;

namespace Shadowsocks.Obfs
{
    public class TlsAuthData
    {
        public byte[] clientID;
        public Dictionary<string, byte[]> ticket_buf;
    }
}
