using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Shadowsocks.Util
{
    public class EnvCheck
    {
        // According to https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed
        // Hard code the path in Registry.
        private static string dotNet45Registry = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full";

        public static bool CheckDotNet45()
        {
            Int32 installed = Convert.ToInt32(Registry.GetValue(dotNet45Registry, "Release", 0));
            if (0 == installed)
                return false;

            if (378389 <= installed)
                return true;

            return false;
        }
    }
}
