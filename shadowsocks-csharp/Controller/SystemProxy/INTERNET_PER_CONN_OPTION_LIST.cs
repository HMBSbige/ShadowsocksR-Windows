using System;
using System.Runtime.InteropServices;

namespace Shadowsocks.Controller.SystemProxy
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct INTERNET_PER_CONN_OPTION_LIST
    {
        public int Size;

        // The connection to be set. NULL means LAN.
        public IntPtr Connection;

        public int OptionCount;
        public int OptionError;

        // List of INTERNET_PER_CONN_OPTION.
        public IntPtr pOptions;
    }
}
