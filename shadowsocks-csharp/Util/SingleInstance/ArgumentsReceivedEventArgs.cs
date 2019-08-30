using System;

namespace Shadowsocks.Util.SingleInstance
{
    public class ArgumentsReceivedEventArgs : EventArgs
    {
        public string[] Args { get; set; }
    }
}
