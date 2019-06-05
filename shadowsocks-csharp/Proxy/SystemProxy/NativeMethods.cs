using System;
using System.Runtime.InteropServices;

namespace Shadowsocks.Proxy.SystemProxy
{
    internal static class NativeMethods
    {
        [DllImport(@"wininet.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
    }
}
