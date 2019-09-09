using System;
using System.Runtime.InteropServices;

namespace Shadowsocks.Controller.SystemProxy
{
    internal static class StringUtils
    {
        internal static IntPtr IntPtrFromString(string managedString)
        {
            return managedString == null ? IntPtr.Zero : Marshal.StringToHGlobalAnsi(managedString);
        }
    }
}
