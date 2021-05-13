using Shadowsocks.Enums;
using Shadowsocks.Util;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Shadowsocks.View.ValueConverter
{
    public class ProxyTypeEnumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ProxyType type)
            {
                return I18NUtil.GetAppStringValue(type.ToString());
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
