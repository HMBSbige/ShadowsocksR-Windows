using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Shadowsocks.Controller.SystemProxy
{
    internal sealed class SetSystemProxy : IDisposable
    {
        private enum ProxyType
        {
            Direct = 1,
            Pac = 2,
            Global = 3
        }

        private static readonly string[] LanIp =
        {
                "<local>",
                "localhost",
                "127.*",
                "10.*",
                "172.16.*",
                "172.17.*",
                "172.18.*",
                "172.19.*",
                "172.20.*",
                "172.21.*",
                "172.22.*",
                "172.23.*",
                "172.24.*",
                "172.25.*",
                "172.26.*",
                "172.27.*",
                "172.28.*",
                "172.29.*",
                "172.30.*",
                "172.31.*",
                "192.168.*"
        };

        private INTERNET_PER_CONN_OPTION_LIST _options;

        public bool Pac(string url)
        {
            Initialize(ref _options, ProxyType.Pac, url);
            return Apply();
        }

        public bool Global(string url, string bypass = @"")
        {
            var customBypassList = new List<string>(bypass.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            customBypassList.AddRange(LanIp);
            var realBypassList = customBypassList.Distinct().ToArray();
            var realBypassString = string.Join(",", realBypassList);
            Initialize(ref _options, ProxyType.Global, url, realBypassString);
            return Apply();
        }

        public bool Direct()
        {
            Initialize(ref _options, ProxyType.Direct);
            return Apply();
        }

        private static void Initialize(ref INTERNET_PER_CONN_OPTION_LIST options, ProxyType type, string url = null, string bypass = @"<local>")
        {
            var optionCount = (int)type;

            var dwBufferSize = Marshal.SizeOf(typeof(INTERNET_PER_CONN_OPTION_LIST));
            options.Size = dwBufferSize;

            options.OptionCount = optionCount;
            options.OptionError = 0;

            var perOption = new INTERNET_PER_CONN_OPTION[optionCount];
            var optSize = Marshal.SizeOf(typeof(INTERNET_PER_CONN_OPTION));
            var optionsPtr = Marshal.AllocCoTaskMem(perOption.Length * optSize);// free

            if (optionsPtr == IntPtr.Zero)
            {
                throw new NullReferenceException();
            }

            perOption[0].dwOption = INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_FLAGS;

            switch (type)
            {
                case ProxyType.Direct:
                {
                    perOption[0].Value.dwValue = INTERNET_OPTION_PER_CONN_FLAGS.PROXY_TYPE_AUTO_DETECT | INTERNET_OPTION_PER_CONN_FLAGS.PROXY_TYPE_DIRECT;
                    break;
                }
                case ProxyType.Pac:
                {
                    perOption[0].Value.dwValue = INTERNET_OPTION_PER_CONN_FLAGS.PROXY_TYPE_AUTO_PROXY_URL | INTERNET_OPTION_PER_CONN_FLAGS.PROXY_TYPE_DIRECT;

                    perOption[1].dwOption = INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_AUTOCONFIG_URL;
                    perOption[1].Value.pszValue = StringUtils.IntPtrFromString(url);
                    break;
                }
                case ProxyType.Global:
                {
                    perOption[0].Value.dwValue = INTERNET_OPTION_PER_CONN_FLAGS.PROXY_TYPE_PROXY | INTERNET_OPTION_PER_CONN_FLAGS.PROXY_TYPE_DIRECT;

                    perOption[1].dwOption = INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_PROXY_SERVER;
                    perOption[1].Value.pszValue = StringUtils.IntPtrFromString(url);

                    perOption[2].dwOption = INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_PROXY_BYPASS;

                    perOption[2].Value.pszValue = StringUtils.IntPtrFromString(bypass);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            // copy the array over into that spot in memory ...
            if (Environment.Is64BitProcess)
            {
                var baseAddress = optionsPtr.ToInt64(); //long
                for (var i = 0; i < perOption.Length; ++i)
                {
                    var opt = new IntPtr(baseAddress + i * optSize);
                    Marshal.StructureToPtr(perOption[i], opt, false);
                }
            }
            else
            {
                var baseAddress = optionsPtr.ToInt32(); //Int
                for (var i = 0; i < perOption.Length; ++i)
                {
                    var opt = new IntPtr(baseAddress + i * optSize);
                    Marshal.StructureToPtr(perOption[i], opt, false);
                }
            }

            options.pOptions = optionsPtr;
        }

        private static bool Apply_connect(ref INTERNET_PER_CONN_OPTION_LIST options, string conn)
        {
            options.Connection = StringUtils.IntPtrFromString(conn);

            var optionsPtr = Marshal.AllocCoTaskMem(options.Size); //free
            Marshal.StructureToPtr(options, optionsPtr, false);

            var ret = NativeMethods.InternetSetOption(IntPtr.Zero, INTERNET_OPTION.INTERNET_OPTION_PER_CONNECTION_OPTION, optionsPtr, Marshal.SizeOf(typeof(INTERNET_PER_CONN_OPTION_LIST)));

            Marshal.FreeCoTaskMem(optionsPtr);

            if (!ret)
            {
                return false;
            }

            if (!NativeMethods.InternetSetOption(IntPtr.Zero, INTERNET_OPTION.INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0))
            {
                return false;
            }

            if (!NativeMethods.InternetSetOption(IntPtr.Zero, INTERNET_OPTION.INTERNET_OPTION_PROXY_SETTINGS_CHANGED, IntPtr.Zero, 0))
            {
                return false;
            }

            if (!NativeMethods.InternetSetOption(IntPtr.Zero, INTERNET_OPTION.INTERNET_OPTION_REFRESH, IntPtr.Zero, 0))
            {
                return false;
            }

            return true;
        }

        private static IEnumerable<string> GetRasEntryNames()
        {
            var cb = Marshal.SizeOf(typeof(RASENTRYNAME));
            var entries = 0;
            var lpRasEntryName = new RASENTRYNAME[1];
            lpRasEntryName[0].Size = Marshal.SizeOf(typeof(RASENTRYNAME));

            NativeMethods.RasEnumEntries(IntPtr.Zero, IntPtr.Zero, lpRasEntryName, ref cb, ref entries);

            if (entries == 0) return new string[0];

            var entryNames = new string[entries];

            lpRasEntryName = new RASENTRYNAME[entries];
            for (var i = 0; i < entries; ++i)
            {
                lpRasEntryName[i].Size = Marshal.SizeOf(typeof(RASENTRYNAME));
            }

            NativeMethods.RasEnumEntries(IntPtr.Zero, IntPtr.Zero, lpRasEntryName, ref cb, ref entries);

            for (var i = 0; i < entries; ++i)
            {
                entryNames[i] = lpRasEntryName[i].EntryName;
            }

            return entryNames;
        }

        private bool Apply()
        {
            var res = true;
            foreach (var name in GetRasEntryNames())
            {
                if (!Apply_connect(ref _options, name))
                {
                    res = false;
                }
            }
            if (!Apply_connect(ref _options, null))
            {
                res = false;
            }
            return res;
        }

        #region IDisposable Support
        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {

                }
                if (_options.pOptions != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(_options.pOptions);
                    _options.pOptions = IntPtr.Zero;
                }
                _disposedValue = true;
            }
        }

        ~SetSystemProxy()
        {
            Dispose(false);
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
