using System;
using System.Runtime.InteropServices;

namespace Shadowsocks.Proxy.SystemProxy
{
    /// <summary>
    /// Used in INTERNET_PER_CONN_OPTION.
    /// When create a instance of OptionUnion, only one filed will be used.
    /// The StructLayout and FieldOffset attributes could help to decrease the struct size.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public class INTERNET_PER_CONN_OPTION_OptionUnion : IDisposable
    {
        // A value in INTERNET_OPTION_PER_CONN_FLAGS.
        [FieldOffset(0)]
        public int dwValue;
        [FieldOffset(0)]
        public IntPtr pszValue;
        [FieldOffset(0)]
        public System.Runtime.InteropServices.ComTypes.FILETIME ftValue;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (pszValue != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pszValue);
                    pszValue = IntPtr.Zero;
                }
            }
        }
    }
}