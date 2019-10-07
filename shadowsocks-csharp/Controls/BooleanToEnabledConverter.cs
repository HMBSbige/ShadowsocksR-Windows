using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Shadowsocks.Controls
{
    public class BooleanToEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool enable)
            {
                if (targetType == typeof(Brush))
                {
                    return enable ? Brushes.LightGreen : Brushes.Red;
                }
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
