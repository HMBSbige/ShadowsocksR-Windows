using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Shadowsocks.Util
{
    public static class I18NUtil
    {
        private const string DefaultLanguage = @"en-US";

        public static string CurrentLanguage;

        public static readonly Dictionary<string, string> SupportLanguage = new Dictionary<string, string>
        {
                {@"简体中文", @"zh-CN"},
                {@"繁体中文", @"zh-TW"},
                {@"English (United States)", @"en-US"},
        };

        public static string GetLanguage(string name)
        {
            return SupportLanguage.All(s => name != s.Value) ? GetLanguage() : name;
        }

        public static string GetLanguage()
        {
            var name = System.Globalization.CultureInfo.CurrentCulture.Name;
            return SupportLanguage.Any(s => name == s.Value) ? name : DefaultLanguage;
        }

        public static void SetLanguage(string langName)
        {
            SetLanguage(Application.Current.Resources, @"App", langName);
            CurrentLanguage = langName;
        }

        public static string GetAppStringValue(string key)
        {
            if (Application.Current.Resources.MergedDictionaries[0][key] is string str)
            {
                return str;
            }
            return key;
        }

        public static string GetWindowStringValue(Window window, string key)
        {
            if (window.Resources.MergedDictionaries[0][key] is string str)
            {
                return str;
            }
            return key;
        }

        public static void SetLanguage(ResourceDictionary resources, string filename, string langName = @"")
        {
            if (string.IsNullOrEmpty(langName))
            {
                langName = GetLanguage();
            }

            var url = new Uri($@"../I18N/{filename}.{langName}.xaml", UriKind.Relative);
            if (resources.MergedDictionaries.Count > 0)
            {
                resources.MergedDictionaries[0].Source = url;
            }
            else if (Application.LoadComponent(url) is ResourceDictionary langRd)
            {
                resources.MergedDictionaries.Add(langRd);
            }
        }

        public static void SetLanguage(object obj)
        {
            if (obj is ContextMenu contextMenu)
            {
                foreach (var o in contextMenu.Items)
                {
                    if (o is MenuItem)
                    {
                        SetLanguage(o);
                    }
                }
            }
            else if (obj is MenuItem menuItem)
            {
                menuItem.Header = GetAppStringValue(menuItem.Name);
                foreach (var o in menuItem.Items)
                {
                    if (o is MenuItem)
                    {
                        SetLanguage(o);
                    }
                }
            }
        }
    }
}
