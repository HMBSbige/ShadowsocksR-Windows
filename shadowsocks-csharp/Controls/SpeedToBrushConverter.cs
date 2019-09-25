using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Shadowsocks.Util;

namespace Shadowsocks.Controls
{
    public class SpeedToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is long bytes)
            {
                return new SolidColorBrush(ColorConvert.GetSpeedColor(bytes));
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
