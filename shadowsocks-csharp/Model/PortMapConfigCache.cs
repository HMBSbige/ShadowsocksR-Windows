using Shadowsocks.Enums;

namespace Shadowsocks.Model
{
    public class PortMapConfigCache
    {
        public PortMapType type;
        public string id;
        public Server server;
        public string server_addr;
        public int server_port;
    }
}
