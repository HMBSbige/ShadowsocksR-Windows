using System;
using System.Collections.Generic;
using System.Text;

namespace Shadowsocks.Util
{
    public static class ServerName
    {
        public static string HideServerAddr(string addr)
        {
            string server_alter_name = addr;
            int pos = addr.LastIndexOf('.');
            if (pos > 0)
            {
                server_alter_name = "*" + addr.Substring(pos);
            }

            return server_alter_name;
        }

        public static string HideServerAddrV6(string addr)
        {
            string server_alter_name = addr;

            int lpos = addr.IndexOf(':');
            int rpos = addr.LastIndexOf(':');

            string san_prefix = addr.Substring(0, lpos);
            string san_suffix = "";
            if (rpos < addr.Length - 1)
            {
                san_suffix = addr.Substring(rpos + 1);
            }

            if (san_prefix.Length > 2)
            {
                string sub = san_prefix.Substring(0, 2);
                san_prefix = sub + "**";
            }

            if (san_suffix.Length > 2)
            {
                string sub = san_suffix.Substring(2);
                san_suffix = "**" + sub;
            }

            server_alter_name = san_prefix + "::" + san_suffix;
            return server_alter_name;
        }
    }
}
