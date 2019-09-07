using Shadowsocks.Controller;
using System;

namespace Shadowsocks.Model
{
    [Serializable]
    public class ServerSubscribe
    {
        public string Url = UpdateFreeNode.DefaultUpdateUrl;
        public string Group;
        public ulong LastUpdateTime;
    }
}