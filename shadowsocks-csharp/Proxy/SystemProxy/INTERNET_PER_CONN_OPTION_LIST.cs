using System;
using System.Runtime.InteropServices;

namespace Shadowsocks.Proxy.SystemProxy
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class INTERNET_PER_CONN_OPTION_LIST : IDisposable
    {
        public int Size;

        // The connection to be set. NULL means LAN.
        public IntPtr Connection;

        public int OptionCount;
        public int OptionError;

        // List of INTERNET_PER_CONN_OPTIONs.
        public IntPtr pOptions;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Connection != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(Connection);
                    Connection = IntPtr.Zero;
                }

                if (pOptions != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pOptions);
                    pOptions = IntPtr.Zero;
                }
            }
        }
    }
}