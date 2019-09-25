using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Shadowsocks.Util;

namespace Shadowsocks.Controls
{
    public class ErrorPercentToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double percent)
            {
                return new SolidColorBrush(ColorConvert.GetErrorPercentColor(percent));
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
                System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}