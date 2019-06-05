using Shadowsocks.Controller;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Shadowsocks.Proxy.SystemProxy
{
    internal static class WinINet
    {
        /// <summary>
        /// Set IE settings.
        /// </summary>
        private static void SetIEProxy(bool enable, bool global, string proxyServer, string pacURL, string connName)
        {
            var optionlist = new List<INTERNET_PER_CONN_OPTION>();

            if (enable)
            {
                if (global)
                {
                    // global proxy
                    optionlist.Add(new INTERNET_PER_CONN_OPTION
                    {
                        dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_FLAGS_UI,
                        Value = { dwValue = (int)(INTERNET_OPTION_PER_CONN_FLAGS_UI.PROXY_TYPE_PROXY
                                                //| INTERNET_OPTION_PER_CONN_FLAGS_UI.PROXY_TYPE_DIRECT
                                                ) }
                    });
                    optionlist.Add(new INTERNET_PER_CONN_OPTION
                    {
                        dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_PROXY_SERVER,
                        Value = { pszValue = Marshal.StringToHGlobalAuto(proxyServer) }
                    });
                    optionlist.Add(new INTERNET_PER_CONN_OPTION
                    {
                        dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_PROXY_BYPASS,
                        Value = { pszValue = Marshal.StringToHGlobalAuto("") }
                    });
                }
                else
                {
                    // pac
                    optionlist.Add(new INTERNET_PER_CONN_OPTION
                    {
                        dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_FLAGS_UI,
                        Value = { dwValue = (int)INTERNET_OPTION_PER_CONN_FLAGS_UI.PROXY_TYPE_AUTO_PROXY_URL }
                    });
                    optionlist.Add(new INTERNET_PER_CONN_OPTION
                    {
                        dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_AUTOCONFIG_URL,
                        Value = { pszValue = Marshal.StringToHGlobalAuto(pacURL) }
                    });
                }
            }
            else
            {
                // direct
                optionlist.Add(new INTERNET_PER_CONN_OPTION
                {
                    dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_FLAGS_UI,
                    Value = { dwValue = (int)(INTERNET_OPTION_PER_CONN_FLAGS_UI.PROXY_TYPE_DIRECT
                                            //| INTERNET_OPTION_PER_CONN_FLAGS_UI.PROXY_TYPE_AUTO_DETECT
                                            ) }
                });
                optionlist.Add(new INTERNET_PER_CONN_OPTION
                {
                    dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_PROXY_BYPASS,
                    Value = { pszValue = Marshal.StringToHGlobalAuto("localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;172.32.*;192.168.*;<local>") }
                });
            }

            // Get total length of INTERNET_PER_CONN_OPTIONs
            var len = 0;
            foreach (var option in optionlist)
            {
                len += Marshal.SizeOf(option);
            }

            // Allocate a block of memory of the options.
            var buffer = Marshal.AllocCoTaskMem(len);

            var current = buffer;

            // Marshal data from a managed object to an unmanaged block of memory.
            foreach (var eachOption in optionlist)
            {
                Marshal.StructureToPtr(eachOption, current, false);
                current = (IntPtr)((long)current + Marshal.SizeOf(eachOption));
            }

            // Initialize a INTERNET_PER_CONN_OPTION_LIST instance.
            var optionList = new INTERNET_PER_CONN_OPTION_LIST();

            // Point to the allocated memory.
            optionList.pOptions = buffer;

            // Return the unmanaged size of an object in bytes.
            optionList.Size = Marshal.SizeOf(optionList);

            optionList.Connection = string.IsNullOrEmpty(connName)
                ? IntPtr.Zero // NULL means LAN
                : Marshal.StringToHGlobalAuto(connName); // TODO: not working if contains Chinese

            optionList.OptionCount = optionlist.Count;
            optionList.OptionError = 0;
            var optionListSize = Marshal.SizeOf(optionList);

            // Allocate memory for the INTERNET_PER_CONN_OPTION_LIST instance.
            var intptrStruct = Marshal.AllocCoTaskMem(optionListSize);

            // Marshal data from a managed object to an unmanaged block of memory.
            Marshal.StructureToPtr(optionList, intptrStruct, true);

            // Set internet settings.
            var bReturn = NativeMethods.InternetSetOption(
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
                foreach (var connName in allConnections)
                {
                    SetIEProxy(enable, global, proxyServer, pacURL, connName);
                }
            }
        }
    }
}
