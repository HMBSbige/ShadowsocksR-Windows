using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shadowsocks.Properties;

namespace Shadowsocks.Controller
{
    public class I18N
    {
        protected static Dictionary<string, string> Strings;

        static void Init(string res)
        {
            var lines = Regex.Split(res, "\r\n|\r|\n");
            foreach (var line in lines)
            {
                if (line.StartsWith("#"))
                {
                    continue;
                }
                var kv = Regex.Split(line, "=");
                if (kv.Length == 2)
                {
                    var val = Regex.Replace(kv[1], "\\\\n", "\r\n");
                    Strings[kv[0]] = val;
                }
            }
        }
        static I18N()
        {
            Strings = new Dictionary<string, string>();

            //if (System.Globalization.CultureInfo.CurrentCulture.IetfLanguageTag.ToLowerInvariant().StartsWith("zh"))
            var name = System.Globalization.CultureInfo.CurrentCulture.Name;
            if (name.StartsWith("zh"))
            {
                if (name == "zh" || name == "zh-CN")
                {
                    Init(Resources.cn);
                }
                else
                {
                    Init(Resources.zh_tw);
                }
            }
        }

        public static string GetString(string key)
        {
            if (Strings.ContainsKey(key))
            {
                return Strings[key];
            }

            return key;
        }
    }
}
