using Shadowsocks.Enums;
using Shadowsocks.Util;
using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Shadowsocks.View.ValueConverter
{
    public class BooleanToEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool enable)
            {
                if (targetType == typeof(Brush))
                {
                    return enable ? ColorConvert.EnableBrush : ColorConvert.DisableBrush;
                }
            }
            else if (value is ServerTreeViewType type)
            {
                if (targetType == typeof(Visibility))
                {
                    return type == ServerTreeViewType.Server ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
