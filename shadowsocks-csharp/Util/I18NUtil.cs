using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Shadowsocks.Util
{
    public static class I18NUtil
    {
        private const string DefaultLanguage = @"en-US";

        public static string CurrentLanguage;

        public static readonly Dictionary<string, string> SupportLanguage = new()
        {
            { @"zh-CN", @"zh-CN" },
            { @"zh", @"zh-CN" },
            { @"zh-Hans", @"zh-CN" },
            { @"zh-SG", @"zh-CN" },
            { @"zh-Hant", @"zh-TW" },
            { @"zh-HK", @"zh-TW" },
            { @"zh-TW", @"zh-TW" },
            { @"zh-MO", @"zh-TW" },
            { @"en-US", @"en-US" }
        };

        public static string GetLanguage(string langName = @"")
        {
            if (string.IsNullOrEmpty(langName))
            {
                langName = System.Globalization.CultureInfo.CurrentCulture.Name;
            }

            if (SupportLanguage.TryGetValue(langName, out var res))
            {
                return res;
            }
            return DefaultLanguage;
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

        public static string GetWindowStringValue(this Window window, string key)
        {
            if (window.Resources.MergedDictionaries[0][key] is string str)
            {
                return str;
            }
            return key;
        }

        public static void SetLanguage(ResourceDictionary resources, string filename, string langName = @"")
        {
            langName = GetLanguage(string.IsNullOrEmpty(langName) ? CurrentLanguage : langName);

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
