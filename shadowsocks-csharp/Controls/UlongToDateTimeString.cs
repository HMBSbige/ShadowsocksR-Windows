using System;
using System.Windows.Data;

namespace Shadowsocks.Controls
{
    public class UlongToDateTimeString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ulong lastUpdateTime && targetType == typeof(string))
            {
                if (lastUpdateTime != 0)
                {
                    var now = new DateTime(1970, 1, 1, 0, 0, 0);
                    now = now.AddSeconds(lastUpdateTime);
                    return $@"{now.ToLongDateString()} {now.ToLongTimeString()}";
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
