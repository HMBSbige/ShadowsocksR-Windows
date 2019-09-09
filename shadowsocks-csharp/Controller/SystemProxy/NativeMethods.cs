using System;
using System.Runtime.InteropServices;

namespace Shadowsocks.Controller.SystemProxy
{
    internal static class NativeMethods
    {
        /// <summary>
        /// Sets an Internet option.
        /// </summary>
        [DllImport(@"WinINet.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        internal static extern bool InternetSetOption(
            IntPtr hInternet,
            INTERNET_OPTION dwOption,
            IntPtr lpBuffer,
            int lpdwBufferLength);
    }
}
