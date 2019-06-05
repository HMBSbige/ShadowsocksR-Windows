using System;

namespace Shadowsocks.Proxy.SystemProxy
{
    /// <summary>
    /// Constants used in INTERNET_PER_CONN_OPTON struct.
    /// Windows 7 and later:  
    /// Clients that support Internet Explorer 8 should query the connection type using INTERNET_PER_CONN_FLAGS_UI.
    /// If this query fails, then the system is running a previous version of Internet Explorer and the client should
    /// query again with INTERNET_PER_CONN_FLAGS.
    /// Restore the connection type using INTERNET_PER_CONN_FLAGS regardless of the version of Internet Explorer.
    /// XXX: If fails, notify user to upgrade Internet Explorer
    /// </summary>
    [Flags]
    public enum INTERNET_OPTION_PER_CONN_FLAGS_UI
    {
        PROXY_TYPE_DIRECT = 0x00000001,   // direct to net
        PROXY_TYPE_PROXY = 0x00000002,   // via named proxy
        PROXY_TYPE_AUTO_PROXY_URL = 0x00000004,   // autoproxy URL
        PROXY_TYPE_AUTO_DETECT = 0x00000008   // use autoproxy detection
    }
}