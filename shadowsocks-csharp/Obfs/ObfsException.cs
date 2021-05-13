using System;

namespace Shadowsocks.Obfs
{
    public class ObfsException : Exception
    {
        public ObfsException(string info)
                : base(info)
        {

        }
    }
}
