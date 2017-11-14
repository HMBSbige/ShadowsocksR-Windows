using System;
using System.Collections.Generic;
using System.Text;

namespace Shadowsocks.Util
{
    public static class ServerName
    {
        public static string HideServerAddr(string addr)
        {
            System.Net.IPAddress ipAddr;
            string serverAlterName = addr;

            bool parsed = System.Net.IPAddress.TryParse(addr, out ipAddr);
            if (parsed)
            {
                char separator;
                if (System.Net.Sockets.AddressFamily.InterNetwork == ipAddr.AddressFamily)
                    separator = '.';  // IPv4
                else
                    separator = ':';  // IPv6

                serverAlterName = HideAddr(addr, separator);
            }
            else
            {
                int pos = addr.IndexOf('.', 1);
                if (pos > 0)
                {
                    serverAlterName = ("*" + addr.Substring(pos));
                }
            }

            return serverAlterName;
        }

        private static string HideAddr(string addr, char separator)
        {
            string result = "";

            string[] splited = addr.Split(separator);
            string prefix = splited[0];
            string suffix = splited[splited.Length - 1];

            if (0 < prefix.Length)
                result = (prefix + separator);

            result += "**";

            if (0 < suffix.Length)
                result += (separator + suffix);

            return result;
        }
    }
}
