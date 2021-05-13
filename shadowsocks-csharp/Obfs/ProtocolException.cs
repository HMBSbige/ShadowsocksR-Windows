using System;

namespace Shadowsocks.Obfs
{
    public class ProtocolException : Exception
    {
        public ProtocolException(string info) : base(info)
        {

        }
    }
}
