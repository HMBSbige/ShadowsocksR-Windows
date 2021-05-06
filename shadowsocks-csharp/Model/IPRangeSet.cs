using RouteMatcher.Abstractions;
using RouteMatcher.IPMatchers;
using Shadowsocks.Enums;
using Shadowsocks.Properties;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Text;

#nullable enable

namespace Shadowsocks.Model
{
    public class IPRangeSet
    {
        public const string ChnFilename = @"chn_ip.txt";
        private static bool IsReverse => Global.GuiConfig.ProxyRuleMode == ProxyRuleMode.BypassLanAndNotChina;

        private IIPAddressMatcher<Rule> _ipMatcher;

        public IPRangeSet()
        {
            Reset();
        }

        [MemberNotNull(nameof(_ipMatcher))]
        private void Reset()
        {
            _ipMatcher = new IPMatcherTrie<Rule>();
        }

        public bool IsInIPRange(IPAddress ip)
        {
            if (!IsReverse)
            {
                return _ipMatcher.Match(ip) == Rule.Direct;
            }

            return _ipMatcher.Match(ip) == default;
        }

        private bool LoadChn(IEnumerable<string?> lines)
        {
            try
            {
                var hasRule = false;
                foreach (var line in lines)
                {
                    if (line is null)
                    {
                        continue;
                    }

                    var strings = line.Split('/', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (strings.Length != 2
                        || !IPAddress.TryParse(strings[0], out var ip)
                        || !byte.TryParse(strings[1], out var mask))
                    {
                        continue;
                    }

                    _ipMatcher.Update(ip, mask, Rule.Direct);
                    hasRule = true;
                }
                return hasRule;
            }
            catch
            {
                return false;
            }
        }

        public void LoadChn()
        {
            var absFilePath = Path.Combine(Directory.GetCurrentDirectory(), ChnFilename);
            if (File.Exists(absFilePath))
            {
                if (!LoadChn(File.ReadLines(absFilePath, Encoding.UTF8)))
                {
                    Reset();
                }
            }
            else
            {
                LoadChn(Resources.chn_ip.GetLines());
            }
        }
    }
}
