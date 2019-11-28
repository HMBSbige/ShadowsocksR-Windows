using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Shadowsocks.Util;

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
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
