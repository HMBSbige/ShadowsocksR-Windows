using System;
using System.Runtime.InteropServices;

namespace Shadowsocks.Controller.SystemProxy
{
    /// <summary>
    /// Constants used in INTERNET_PER_CONN_OPTION_OptionUnion struct.
    /// </summary>
    internal enum INTERNET_PER_CONN_OptionEnum
    {
        INTERNET_PER_CONN_FLAGS = 1,
        INTERNET_PER_CONN_PROXY_SERVER = 2,
        INTERNET_PER_CONN_PROXY_BYPASS = 3,
        INTERNET_PER_CONN_AUTOCONFIG_URL = 4,
        INTERNET_PER_CONN_AUTODISCOVERY_FLAGS = 5,
        INTERNET_PER_CONN_AUTOCONFIG_SECONDARY_URL = 6,
        INTERNET_PER_CONN_AUTOCONFIG_RELOAD_DELAY_MINS = 7,
        INTERNET_PER_CONN_AUTOCONFIG_LAST_DETECT_TIME = 8,
        INTERNET_PER_CONN_AUTOCONFIG_LAST_DETECT_URL = 9,
        INTERNET_PER_CONN_FLAGS_UI = 10
    }

    /// <summary>
    /// Constants used in INTERNET_PER_CONN_OPTON struct.
    /// </summary>
    [Flags]
    internal enum INTERNET_OPTION_PER_CONN_FLAGS
    {
        PROXY_TYPE_DIRECT = 0x00000001,   // direct to net
        PROXY_TYPE_PROXY = 0x00000002,   // via named proxy
        PROXY_TYPE_AUTO_PROXY_URL = 0x00000004,   // autoproxy URL
        PROXY_TYPE_AUTO_DETECT = 0x00000008   // use autoproxy detection
    }

    /// <summary>
    /// Used in INTERNET_PER_CONN_OPTION.
    /// When create a instance of OptionUnion, only one filed will be used.
    /// The StructLayout and FieldOffset attributes could help to decrease the struct size.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct INTERNET_PER_CONN_OPTION_OptionUnion
    {
        // A value in INTERNET_OPTION_PER_CONN_FLAGS.
        [FieldOffset(0)]
        public INTERNET_OPTION_PER_CONN_FLAGS dwValue;
        [FieldOffset(0)]
        public IntPtr pszValue;
        [FieldOffset(0)]
        public System.Runtime.InteropServices.ComTypes.FILETIME ftValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INTERNET_PER_CONN_OPTION
    {
        // A value in INTERNET_PER_CONN_OptionEnum.
        public INTERNET_PER_CONN_OptionEnum dwOption;
        public INTERNET_PER_CONN_OPTION_OptionUnion Value;
    }
}
