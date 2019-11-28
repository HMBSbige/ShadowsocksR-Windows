using System.Runtime.InteropServices;

namespace Shadowsocks.Controller.SystemProxy
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct RASENTRYNAME
    {
        public int Size;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NativeMethods.RasMaxEntryName + 1)]
        public string EntryName;

        public int Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NativeMethods.MAX_PATH + 1)]
        public string Phonebook;
    }
}
