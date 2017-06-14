using System;
using System.Collections.Generic;
using System.Text;

namespace Shadowsocks.Model
{
    class HostNode
    {
        public bool include_sub;
        public string addr;
        public Dictionary<string, HostNode> subnode;

        public HostNode()
        {
            include_sub = false;
            addr = "";
            subnode = new Dictionary<string, HostNode>();
        }

        public HostNode(bool sub, string addr)
        {
            include_sub = sub;
            this.addr = addr;
            subnode = null;
        }
    }

    public class HostMap
    {
        Dictionary<string, HostNode> root = new Dictionary<string, HostNode>();
        static HostMap instance = new HostMap();
        const string HOST_FILENAME = "host.txt";

        public static HostMap Instance()
        {
            return instance;
        }

        public void Clear(HostMap newInstance)
        {
            if (newInstance == null)
            {
                instance = new HostMap();
            }
            else
            {
                instance = newInstance;
            }
        }

        public void AddHost(string host, string addr)
        {
            string[] parts = host.Split('.');
            Dictionary<string, HostNode> node = root;
            bool include_sub = false;
            int end = 0;
            if (parts[0].Length == 0)
            {
                end = 1;
                include_sub = true;
            }
            for (int i = parts.Length - 1; i > end; --i)
            {
                if (!node.ContainsKey(parts[i]))
                {
                    node[parts[i]] = new HostNode();
                }
                if (node[parts[i]].subnode == null)
                {
                    node[parts[i]].subnode = new Dictionary<string, HostNode>();
                }
                node = node[parts[i]].subnode;
            }
            node[parts[end]] = new HostNode(include_sub, addr);
        }

        public bool GetHost(string host, out string addr)
        {
            string[] parts = host.Split('.');
            Dictionary<string, HostNode> node = root;
            addr = null;
            for (int i = parts.Length - 1; i >= 0; --i)
            {
                if (!node.ContainsKey(parts[i]))
                {
                    return false;
                }
                if (node[parts[i]].addr.Length > 0 || node[parts[i]].include_sub)
                {
                    addr = node[parts[i]].addr;
                    return true;
                }
                if (node.ContainsKey("*"))
                {
                    addr = node["*"].addr;
                    return true;
                }
                if (node[parts[i]].subnode == null)
                {
                    return false;
                }
                node = node[parts[i]].subnode;
            }
            return false;
        }

        public bool LoadHostFile()
        {
            string filename = HOST_FILENAME;
            string absFilePath = System.IO.Path.Combine(System.Windows.Forms.Application.StartupPath, filename);
            if (System.IO.File.Exists(absFilePath))
            {
                try
                {
                    using (System.IO.StreamReader stream = System.IO.File.OpenText(absFilePath))
                    {
                        while (true)
                        {
                            string line = stream.ReadLine();
                            if (line == null)
                                break;
                            if (line.Length > 0 && line.StartsWith("#"))
                                continue;
                            string[] parts = line.Split(new char[] { ' ', '\t', }, 2, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length < 2)
                                continue;
                            AddHost(parts[0], parts[1]);
                        }
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }
    }
}
