using System.Collections.Generic;

namespace Shadowsocks.Model
{
    internal class HostNode
    {
        public readonly bool IncludeSub;
        public readonly string Addr;
        public Dictionary<string, HostNode> SubNode;

        public HostNode()
        {
            IncludeSub = false;
            Addr = string.Empty;
            SubNode = new Dictionary<string, HostNode>();
        }

        public HostNode(bool sub, string addr)
        {
            IncludeSub = sub;
            Addr = addr;
            SubNode = null;
        }
    }
}
