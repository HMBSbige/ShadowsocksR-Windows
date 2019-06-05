using System.Runtime.InteropServices;

namespace Shadowsocks.Proxy.SystemProxy
{
    [StructLayout(LayoutKind.Sequential)]
    public struct INTERNET_PER_CONN_OPTION
    {
        // A value in INTERNET_PER_CONN_OptionEnum.
        public int dwOption;
        public INTERNET_PER_CONN_OPTION_OptionUnion Value;
    }
}