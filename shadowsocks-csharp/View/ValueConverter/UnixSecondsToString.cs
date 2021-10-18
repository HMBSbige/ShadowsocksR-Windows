using System;
using System.Windows.Data;

namespace Shadowsocks.View.ValueConverter
{
    public class UnixSecondsToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is long lastUpdateTime && targetType == typeof(string))
            {
                if (lastUpdateTime is not 0)
                {
                    return DateTimeOffset.FromUnixTimeSeconds(lastUpdateTime).ToLocalTime().ToString();
                }
            }
            return @"(｀・ω・´)";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
