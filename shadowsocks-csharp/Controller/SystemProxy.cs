using System.Windows.Forms;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using Shadowsocks.Model;

using System.ComponentModel;

namespace Shadowsocks.Controller
{
    public enum INTERNET_OPTION
    {
        // Sets or retrieves an INTERNET_PER_CONN_OPTION_LIST structure that specifies
        // a list of options for a particular connection.
        INTERNET_OPTION_PER_CONNECTION_OPTION = 75,

        // Notify the system that the registry settings have been changed so that
        // it verifies the settings on the next call to InternetConnect.
        INTERNET_OPTION_SETTINGS_CHANGED = 39,

        // Causes the proxy data to be reread from the registry for a handle.
        INTERNET_OPTION_REFRESH = 37,

        // Alerts the current WinInet instance that proxy settings have changed
        // and that they must update with the new settings.
        // To alert all available WinInet instances, set the Buffer parameter of
        // InternetSetOption to NULL and BufferLength to 0 when passing this option.
        INTERNET_OPTION_PROXY_SETTINGS_CHANGED = 95

    }

    /// <summary>
    /// Constants used in INTERNET_PER_CONN_OPTION_OptionUnion struct.
    /// </summary>
    public enum INTERNET_PER_CONN_OptionEnum
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
    public enum INTERNET_OPTION_PER_CONN_FLAGS
    {
        PROXY_TYPE_DIRECT = 0x00000001,   // direct to net
        PROXY_TYPE_PROXY = 0x00000002,   // via named proxy
        PROXY_TYPE_AUTO_PROXY_URL = 0x00000004,   // autoproxy URL
        PROXY_TYPE_AUTO_DETECT = 0x00000008   // use autoproxy detection
    }

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

    /// <summary>
    /// Used in INTERNET_PER_CONN_OPTION.
    /// When create a instance of OptionUnion, only one filed will be used.
    /// The StructLayout and FieldOffset attributes could help to decrease the struct size.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct INTERNET_PER_CONN_OPTION_OptionUnion : IDisposable
    {
        // A value in INTERNET_OPTION_PER_CONN_FLAGS.
        [FieldOffset(0)]
        public int dwValue;
        [FieldOffset(0)]
        public System.IntPtr pszValue;
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

    [StructLayout(LayoutKind.Sequential)]
    public struct INTERNET_PER_CONN_OPTION
    {
        // A value in INTERNET_PER_CONN_OptionEnum.
        public int dwOption;
        public INTERNET_PER_CONN_OPTION_OptionUnion Value;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct INTERNET_PER_CONN_OPTION_LIST : IDisposable
    {
        public int Size;

        // The connection to be set. NULL means LAN.
        public System.IntPtr Connection;

        public int OptionCount;
        public int OptionError;

        // List of INTERNET_PER_CONN_OPTIONs.
        public System.IntPtr pOptions;

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
    public static class WinINet
    {
        /// <summary>
        /// Set IE settings.
        /// </summary>
        private static void SetIEProxy(bool enable, bool global, string proxyServer, string pacURL, string connName)
        {
            List<INTERNET_PER_CONN_OPTION> _optionlist = new List<INTERNET_PER_CONN_OPTION>();

            if (enable)
            {
                if (global)
                {
                    // global proxy
                    _optionlist.Add(new INTERNET_PER_CONN_OPTION
                    {
                        dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_FLAGS_UI,
                        Value = { dwValue = (int)(INTERNET_OPTION_PER_CONN_FLAGS_UI.PROXY_TYPE_PROXY
                                                //| INTERNET_OPTION_PER_CONN_FLAGS_UI.PROXY_TYPE_DIRECT
                                                ) }
                    });
                    _optionlist.Add(new INTERNET_PER_CONN_OPTION
                    {
                        dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_PROXY_SERVER,
                        Value = { pszValue = Marshal.StringToHGlobalAuto(proxyServer) }
                    });
                    _optionlist.Add(new INTERNET_PER_CONN_OPTION
                    {
                        dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_PROXY_BYPASS,
                        Value = { pszValue = Marshal.StringToHGlobalAuto("") }
                    });
                }
                else
                {
                    // pac
                    _optionlist.Add(new INTERNET_PER_CONN_OPTION
                    {
                        dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_FLAGS_UI,
                        Value = { dwValue = (int)INTERNET_OPTION_PER_CONN_FLAGS_UI.PROXY_TYPE_AUTO_PROXY_URL }
                    });
                    _optionlist.Add(new INTERNET_PER_CONN_OPTION
                    {
                        dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_AUTOCONFIG_URL,
                        Value = { pszValue = Marshal.StringToHGlobalAuto(pacURL) }
                    });
                }
            }
            else
            {
                // direct
                _optionlist.Add(new INTERNET_PER_CONN_OPTION
                {
                    dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_FLAGS_UI,
                    Value = { dwValue = (int)(INTERNET_OPTION_PER_CONN_FLAGS_UI.PROXY_TYPE_DIRECT
                                            //| INTERNET_OPTION_PER_CONN_FLAGS_UI.PROXY_TYPE_AUTO_DETECT
                                            ) }
                });
                _optionlist.Add(new INTERNET_PER_CONN_OPTION
                {
                    dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_PROXY_BYPASS,
                    Value = { pszValue = Marshal.StringToHGlobalAuto("localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;172.32.*;192.168.*;<local>") }
                });
            }

            // Get total length of INTERNET_PER_CONN_OPTIONs
            int len = 0;
            foreach(INTERNET_PER_CONN_OPTION option in _optionlist)
            {
                len += Marshal.SizeOf(option);
            }

            // Allocate a block of memory of the options.
            IntPtr buffer = Marshal.AllocCoTaskMem(len);

            IntPtr current = buffer;

            // Marshal data from a managed object to an unmanaged block of memory.
            foreach (INTERNET_PER_CONN_OPTION eachOption in _optionlist)
            {
                Marshal.StructureToPtr(eachOption, current, false);
                current = (IntPtr)((long)current + Marshal.SizeOf(eachOption));
            }

            // Initialize a INTERNET_PER_CONN_OPTION_LIST instance.
            INTERNET_PER_CONN_OPTION_LIST optionList = new INTERNET_PER_CONN_OPTION_LIST();

            // Point to the allocated memory.
            optionList.pOptions = buffer;

            // Return the unmanaged size of an object in bytes.
            optionList.Size = Marshal.SizeOf(optionList);

            optionList.Connection = String.IsNullOrEmpty(connName)
                ? IntPtr.Zero // NULL means LAN
                : Marshal.StringToHGlobalAuto(connName); // TODO: not working if contains Chinese

            optionList.OptionCount = _optionlist.Count;
            optionList.OptionError = 0;
            int optionListSize = Marshal.SizeOf(optionList);

            // Allocate memory for the INTERNET_PER_CONN_OPTION_LIST instance.
            IntPtr intptrStruct = Marshal.AllocCoTaskMem(optionListSize);

            // Marshal data from a managed object to an unmanaged block of memory.
            Marshal.StructureToPtr(optionList, intptrStruct, true);

            // Set internet settings.
            bool bReturn = NativeMethods.InternetSetOption(
                IntPtr.Zero,
                (int)INTERNET_OPTION.INTERNET_OPTION_PER_CONNECTION_OPTION,
                intptrStruct, optionListSize);

            // Free the allocated memory.
            Marshal.FreeCoTaskMem(buffer);
            Marshal.FreeCoTaskMem(intptrStruct);

            // Throw an exception if this operation failed.
            if (!bReturn)
            {
                throw new Exception("InternetSetOption failed.", new Win32Exception());
            }

            // Notify the system that the registry settings have been changed and cause
            // the proxy data to be reread from the registry for a handle.
            bReturn = NativeMethods.InternetSetOption(
                IntPtr.Zero,
                (int)INTERNET_OPTION.INTERNET_OPTION_PROXY_SETTINGS_CHANGED,
                IntPtr.Zero, 0);
            if (!bReturn)
            {
                Logging.Error("InternetSetOption:INTERNET_OPTION_PROXY_SETTINGS_CHANGED");
            }

            bReturn = NativeMethods.InternetSetOption(
                IntPtr.Zero,
                (int)INTERNET_OPTION.INTERNET_OPTION_REFRESH,
                IntPtr.Zero, 0);
            if (!bReturn)
            {
                Logging.Error("InternetSetOption:INTERNET_OPTION_REFRESH");
            }
        }

        public static void SetIEProxy(bool enable, bool global, string proxyServer, string pacURL)
        {
            string[] allConnections = null;
            var ret = RemoteAccessService.GetAllConns(ref allConnections);

            if (ret == 2)
                throw new Exception("Cannot get all connections");

            if (ret == 1)
            {
                // no entries, only set LAN
                SetIEProxy(enable, global, proxyServer, pacURL, null);
            }
            else if (ret == 0)
            {
                // found entries, set LAN and each connection
                SetIEProxy(enable, global, proxyServer, pacURL, null);
                foreach (string connName in allConnections)
                {
                    SetIEProxy(enable, global, proxyServer, pacURL, connName);
                }
            }
        }
    }
    internal static class RemoteAccessService
    {
        private enum RasFieldSizeConstants
        {
            #region original header

            //#if (WINVER >= 0x400)
            //#define RAS_MaxEntryName      256
            //#define RAS_MaxDeviceName     128
            //#define RAS_MaxCallbackNumber RAS_MaxPhoneNumber
            //#else
            //#define RAS_MaxEntryName      20
            //#define RAS_MaxDeviceName     32
            //#define RAS_MaxCallbackNumber 48
            //#endif

            #endregion

            RAS_MaxEntryName = 256,
            RAS_MaxPath = 260
        }

        private const int ERROR_SUCCESS = 0;
        private const int RASBASE = 600;
        private const int ERROR_BUFFER_TOO_SMALL = RASBASE + 3;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct RasEntryName
        {
            #region original header

            //#define RASENTRYNAMEW struct tagRASENTRYNAMEW
            //RASENTRYNAMEW
            //{
            //    DWORD dwSize;
            //    WCHAR szEntryName[RAS_MaxEntryName + 1];
            //
            //#if (WINVER >= 0x500)
            //    //
            //    // If this flag is REN_AllUsers then its a
            //    // system phonebook.
            //    //
            //    DWORD dwFlags;
            //    WCHAR szPhonebookPath[MAX_PATH + 1];
            //#endif
            //};
            //
            //#define RASENTRYNAMEA struct tagRASENTRYNAMEA
            //RASENTRYNAMEA
            //{
            //    DWORD dwSize;
            //    CHAR szEntryName[RAS_MaxEntryName + 1];
            //
            //#if (WINVER >= 0x500)
            //    DWORD dwFlags;
            //    CHAR  szPhonebookPath[MAX_PATH + 1];
            //#endif
            //};

            #endregion

            public int dwSize;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = (int)RasFieldSizeConstants.RAS_MaxEntryName + 1)]
            public string szEntryName;

            public int dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = (int)RasFieldSizeConstants.RAS_MaxPath + 1)]
            public string szPhonebookPath;
        }

        [DllImport("rasapi32.dll", CharSet = CharSet.Auto)]
        private static extern uint RasEnumEntries(
            // reserved, must be NULL
            string reserved,
            // pointer to full path and file name of phone-book file
            string lpszPhonebook,
            // buffer to receive phone-book entries
            [In, Out] RasEntryName[] lprasentryname,
            // size in bytes of buffer
            ref int lpcb,
            // number of entries written to buffer
            out int lpcEntries
        );

        /// <summary>
        /// Get all entries from RAS
        /// </summary>
        /// <param name="allConns"></param>
        /// <returns>
        /// 0: success with entries
        /// 1: success but no entries found
        /// 2: failed
        /// </returns>
        public static uint GetAllConns(ref string[] allConns)
        {
            int lpNames = 0;
            int entryNameSize = 0;
            int lpSize = 0;
            uint retval = ERROR_SUCCESS;
            RasEntryName[] names = null;

            entryNameSize = Marshal.SizeOf(typeof(RasEntryName));

            // Windows Vista or later:  To determine the required buffer size, call RasEnumEntries
            // with lprasentryname set to NULL. The variable pointed to by lpcb should be set to zero.
            // The function will return the required buffer size in lpcb and an error code of ERROR_BUFFER_TOO_SMALL.
            retval = RasEnumEntries(null, null, null, ref lpSize, out lpNames);

            if (retval == ERROR_BUFFER_TOO_SMALL)
            {
                names = new RasEntryName[lpNames];
                for (int i = 0; i < names.Length; i++)
                {
                    names[i].dwSize = entryNameSize;
                }

                retval = RasEnumEntries(null, null, names, ref lpSize, out lpNames);
            }

            if (retval == ERROR_SUCCESS)
            {
                if (lpNames == 0)
                {
                    // no entries found.
                    return 1;
                }

                allConns = new string[names.Length];

                for (int i = 0; i < names.Length; i++)
                {
                    allConns[i] = names[i].szEntryName;
                }
                return 0;
            }
            else
            {
                return 2;
            }
        }
    }

    public class SystemProxy
    {

        //public const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        //public const int INTERNET_OPTION_REFRESH = 37;
        static bool _settingsReturn, _refreshReturn;

        public static void NotifyIE()
        {
            // These lines implement the Interface in the beginning of program 
            // They cause the OS to refresh the settings, causing IP to realy update
            _settingsReturn = NativeMethods.InternetSetOption(IntPtr.Zero, (int)INTERNET_OPTION.INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            _refreshReturn = NativeMethods.InternetSetOption(IntPtr.Zero, (int)INTERNET_OPTION.INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }

        public static void RegistrySetValue(RegistryKey registry, string name, object value)
        {
            try
            {
                registry.SetValue(name, value);
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
            }
        }
        public static RegistryKey OpenUserRegKey(string name, bool writable)
        {
            // we are building x86 binary for both x86 and x64, which will
            // cause problem when opening registry key
            // detect operating system instead of CPU
#if _DOTNET_4_0
            RegistryKey userKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentUser, "",
                Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32
                ).OpenSubKey(name, writable);
#else
            RegistryKey userKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentUser, ""
                ).OpenSubKey(name, writable);
#endif
            return userKey;
        }

        public static void Update(Configuration config, bool forceDisable)
        {
            int sysProxyMode = config.sysProxyMode;
            if (sysProxyMode == (int)ProxyMode.NoModify)
            {
                return;
            }
            if (forceDisable)
            {
                sysProxyMode = (int)ProxyMode.Direct;
            }
            bool global = sysProxyMode == (int)ProxyMode.Global;
            bool enabled = sysProxyMode != (int)ProxyMode.Direct;
            Version win8 = new Version("6.2");
            //if (Environment.OSVersion.Version.CompareTo(win8) < 0)
            {
                using (RegistryKey registry = OpenUserRegKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    try
                    {
                        if (enabled)
                        {
                            if (global)
                            {
                                RegistrySetValue(registry, "ProxyEnable", 1);
                                RegistrySetValue(registry, "ProxyServer", "127.0.0.1:" + config.localPort.ToString());
                                RegistrySetValue(registry, "AutoConfigURL", "");
                            }
                            else
                            {
                                string pacUrl;
                                pacUrl = "http://127.0.0.1:" + config.localPort.ToString() + "/pac?" + "auth=" + config.localAuthPassword + "&t=" + Util.Utils.GetTimestamp(DateTime.Now);
                                RegistrySetValue(registry, "ProxyEnable", 0);
                                RegistrySetValue(registry, "ProxyServer", "");
                                RegistrySetValue(registry, "AutoConfigURL", pacUrl);
                            }
                        }
                        else
                        {
                            RegistrySetValue(registry, "ProxyEnable", 0);
                            RegistrySetValue(registry, "ProxyServer", "");
                            RegistrySetValue(registry, "AutoConfigURL", "");
                        }
                        IEProxyUpdate(config, sysProxyMode);
                        SystemProxy.NotifyIE();
                        //Must Notify IE first, or the connections do not chanage
                        CopyProxySettingFromLan();
                    }
                    catch (Exception e)
                    {
                        Logging.LogUsefulException(e);
                        // TODO this should be moved into views
                        //MessageBox.Show(I18N.GetString("Failed to update registry"));
                    }
                }
            }
            if (Environment.OSVersion.Version.CompareTo(win8) >= 0)
            {
                try
                {
                    if (enabled)
                    {
                        if (global)
                        {
                            WinINet.SetIEProxy(true, true, "127.0.0.1:" + config.localPort.ToString(), "");
                        }
                        else
                        {
                            string pacUrl;
                            pacUrl = $"http://127.0.0.1:{config.localPort}/pac?auth={config.localAuthPassword}&t={Util.Utils.GetTimestamp(DateTime.Now)}";
                            WinINet.SetIEProxy(true, false, "", pacUrl);
                        }
                    }
                    else
                    {
                        WinINet.SetIEProxy(false, false, "", "");
                    }
                }
                catch (Exception ex)
                {
                    Logging.LogUsefulException(ex);
                }
            }
        }

        private static void CopyProxySettingFromLan()
        {
            using (RegistryKey registry = OpenUserRegKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Connections", true))
            {
                try
                {
                    var defaultValue = registry.GetValue("DefaultConnectionSettings");
                    var connections = registry.GetValueNames();
                    foreach (String each in connections)
                    {
                        switch (each.ToUpperInvariant())
                        {
                            case "DEFAULTCONNECTIONSETTINGS":
                            case "SAVEDLEGACYSETTINGS":
                            //case "LAN CONNECTION":
                                continue;
                            default:
                                //set all the connections's proxy as the lan
                                registry.SetValue(each, defaultValue);
                                continue;
                        }
                    }
                    SystemProxy.NotifyIE();
                }
                catch (IOException e)
                {
                    Logging.LogUsefulException(e);
                }
            }
        }

        private static void BytePushback(byte[] buffer, ref int buffer_len, int val)
        {
            BitConverter.GetBytes(val).CopyTo(buffer, buffer_len);
            buffer_len += 4;
        }

        private static void BytePushback(byte[] buffer, ref int buffer_len, string str)
        {
            BytePushback(buffer, ref buffer_len, str.Length);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str);
            bytes.CopyTo(buffer, buffer_len);
            buffer_len += bytes.Length;
        }

        private static byte[] GenConnectionSettings(Configuration config, int sysProxyMode, int counter)
        {
            byte[] buffer = new byte[1024];
            int buffer_len = 0;
            BytePushback(buffer, ref buffer_len, 70);
            BytePushback(buffer, ref buffer_len, counter + 1);
            if (sysProxyMode == (int)ProxyMode.Direct)
                BytePushback(buffer, ref buffer_len, 1);
            else if (sysProxyMode == (int)ProxyMode.Pac)
                BytePushback(buffer, ref buffer_len, 5);
            else
                BytePushback(buffer, ref buffer_len, 3);

            string proxy = "127.0.0.1:" + config.localPort.ToString();
            BytePushback(buffer, ref buffer_len, proxy);

            string bypass = sysProxyMode == (int)ProxyMode.Global ? "" : "localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;172.32.*;192.168.*;<local>";
            BytePushback(buffer, ref buffer_len, bypass);

            string pacUrl = "";
            pacUrl = "http://127.0.0.1:" + config.localPort.ToString() + "/pac?" + "auth=" + config.localAuthPassword + "&t=" + Util.Utils.GetTimestamp(DateTime.Now);
            BytePushback(buffer, ref buffer_len, pacUrl);

            buffer_len += 0x20;

            Array.Resize(ref buffer, buffer_len);
            return buffer;
        }

        /// <summary>
        /// Checks or unchecks the IE Options Connection setting of "Automatically detect Proxy"
        /// </summary>
        private static void IEProxyUpdate(Configuration config, int sysProxyMode)
        {
            using (RegistryKey registry = OpenUserRegKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Connections", true))
            {
                try
                {
                    byte[] defConnection = (byte[])registry.GetValue("DefaultConnectionSettings");
                    int counter = 0;
                    if (defConnection != null && defConnection.Length >= 8)
                    {
                        counter = defConnection[4] | (defConnection[5] << 8);
                    }
                    defConnection = GenConnectionSettings(config, sysProxyMode, counter);
                    RegistrySetValue(registry, "DefaultConnectionSettings", defConnection);
                    RegistrySetValue(registry, "SavedLegacySettings", defConnection);
                }
                catch (IOException e)
                {
                    Logging.LogUsefulException(e);
                }
            }
            using (RegistryKey registry = OpenUserRegKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
            {
                try
                {
                    RegistrySetValue(registry, "ProxyOverride", "localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;172.32.*;192.168.*;<local>");
                }
                catch (IOException e)
                {
                    Logging.LogUsefulException(e);
                }
            }
        }
    }
    internal static class NativeMethods
    {
        [DllImport("wininet.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
    }
}
