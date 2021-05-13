using Shadowsocks.Util;
using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Shadowsocks.View.ValueConverter
{
    public class ConnectEmptyToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is long bytes && bytes > 0)
            {
                return new SolidColorBrush(ColorConvert.GetConnectEmptyColor(bytes));
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
