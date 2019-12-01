using System;
using System.Runtime.InteropServices;

namespace Shadowsocks.Controller.SystemProxy
{
    internal static class NativeMethods
    {
        /// <summary>
        /// Sets an Internet option.
        /// </summary>
        [DllImport(@"WinINet.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        internal static extern bool InternetSetOption(
            IntPtr hInternet,
            INTERNET_OPTION dwOption,
            IntPtr lpBuffer,
            int lpdwBufferLength);

        /// <summary>
        /// Lists all entry names in a remote access phone book.
        /// </summary>
        [DllImport(@"RasApi32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        internal static extern uint RasEnumEntries(
                IntPtr reserved,
                IntPtr lpszPhoneBook,
                [In, Out] RASENTRYNAME[] lpRasEntryName,
                ref int lpcb,
                ref int lpcEntries);

        public const int MAX_PATH = 260;

        public const int RasMaxEntryName = 256;
    }
}
