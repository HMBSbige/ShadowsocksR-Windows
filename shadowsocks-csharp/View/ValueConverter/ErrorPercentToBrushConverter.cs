using Shadowsocks.Util;
using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Shadowsocks.View.ValueConverter
{
    public class ErrorPercentToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double percent && percent > 0)
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
