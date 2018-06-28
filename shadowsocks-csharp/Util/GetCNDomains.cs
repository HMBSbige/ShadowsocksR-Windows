using System;
using System.Collections.Generic;
using System.Text;

namespace Shadowsocks.Util
{
    internal static class GetCNDomains
    {
        public static string GetPACwhitedomains(IEnumerable<string> domains)
        {
            var m = new SortedDictionary<string, HashSet<string>>();
            foreach (var domain in domains)
            {
                var lastIndexOfdot = domain.LastIndexOf('.', domain.Length - 1);
                if (lastIndexOfdot == -1)
                {
                    continue;
                }
                var secondlevel = domain.Remove(lastIndexOfdot);
                var toplevel = domain.Substring(lastIndexOfdot + 1);
                if (!string.IsNullOrWhiteSpace(secondlevel) && !string.IsNullOrWhiteSpace(toplevel))
                {
                    if (m.ContainsKey(toplevel))
                    {
                        m[toplevel].Add(secondlevel);
                    }
                    else
                    {
                        m[toplevel] = new HashSet<string> { secondlevel };
                    }
                }
            }
            var sb = new StringBuilder();
            foreach (var domain in m)
            {
                var ssb = new StringBuilder();
                foreach (var seconddomain in domain.Value)
                {
                    ssb.AppendFormat("\"{0}\":1,\n", seconddomain);
                }
                ssb.Remove(ssb.Length - 2, 2);
                ssb.Append("\n");
                sb.AppendFormat("\"{0}\":{{\n{1}}},", domain.Key, ssb);
            }
            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }

        private static string GetDomainFromLine(string str)
        {
            if (str.Length > 0 && str[0] == '#')
            {
                return null;
            }
            var sArray = str.Split('/');
            return sArray[1];
        }

        public static IEnumerable<string> ReadFromString(string str)
        {
            var domains = new List<string>();

            var lines = str.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var domain = GetDomainFromLine(line);
                if (!string.IsNullOrWhiteSpace(domain))
                {
                    domains.Add(domain);
                }
            }
            return domains.Count == 0 ? null : domains;
        }
    }
}
