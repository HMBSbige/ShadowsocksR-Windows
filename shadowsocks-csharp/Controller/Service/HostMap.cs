using Shadowsocks.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Shadowsocks.Controller.Service
{
    public static class HostMap
    {
        private static readonly Dictionary<string, HostNode> Root = new Dictionary<string, HostNode>();
        private static IPSegment _ips;

        public const string UserRule = @"user.rule";

        static HostMap()
        {
            Clear();
        }

        private static void Clear()
        {
            Root.Clear();
            _ips = new IPSegment(@"remoteproxy");
        }

        public static void Reload()
        {
            Clear();
            LoadHostFile();
        }

        private static void AddHost(string host, string addr)
        {
            if (IPAddress.TryParse(host, out _))
            {
                var addr_parts = addr.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (addr_parts.Length >= 2)
                {
                    _ips.insert(new IPAddressCmp(host), new IPAddressCmp(addr_parts[0]), addr_parts[1]);
                    return;
                }
            }

            var parts = host.Split('.');
            var node = Root;
            var include_sub = false;
            var end = 0;
            if (parts[0].Length == 0)
            {
                end = 1;
                include_sub = true;
            }
            for (var i = parts.Length - 1; i > end; --i)
            {
                if (!node.ContainsKey(parts[i]))
                {
                    node[parts[i]] = new HostNode();
                }
                if (node[parts[i]].SubNode == null)
                {
                    node[parts[i]].SubNode = new Dictionary<string, HostNode>();
                }
                node = node[parts[i]].SubNode;
            }
            node[parts[end]] = new HostNode(include_sub, addr);
        }

        public static bool GetHost(string host, out string addr)
        {
            var parts = host.Split('.');
            var node = Root;
            addr = null;
            for (var i = parts.Length - 1; i >= 0; --i)
            {
                if (!node.ContainsKey(parts[i]))
                {
                    return false;
                }
                if (node[parts[i]].Addr.Length > 0 || node[parts[i]].IncludeSub)
                {
                    addr = node[parts[i]].Addr;
                    return true;
                }
                if (node.ContainsKey("*"))
                {
                    addr = node["*"].Addr;
                    return true;
                }
                if (node[parts[i]].SubNode == null)
                {
                    return false;
                }
                node = node[parts[i]].SubNode;
            }
            return false;
        }

        public static bool GetIP(IPAddress ip, out string addr)
        {
            var host = ip.ToString();
            addr = _ips.Get(new IPAddressCmp(host)) as string;
            return addr != null;
        }

        private static void LoadHostFile()
        {
            if (File.Exists(UserRule))
            {
                try
                {
                    foreach (var line in File.ReadLines(UserRule))
                    {
                        if (line.Length > 0 && line.StartsWith(@"#"))
                            continue;
                        var parts = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2)
                            continue;
                        AddHost(parts[0], parts[1]);
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
