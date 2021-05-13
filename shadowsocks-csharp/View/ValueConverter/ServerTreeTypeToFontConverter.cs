using Shadowsocks.Enums;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Shadowsocks.View.ValueConverter
{
    public class ServerTreeTypeToFontConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ServerTreeViewType type)
            {
                if (targetType == typeof(Brush))
                {
                    switch (type)
                    {
                        case ServerTreeViewType.Subtag:
                            return Brushes.Green;
                        case ServerTreeViewType.Group:
                            return Brushes.DarkOrange;
                        case ServerTreeViewType.Server:
                            break;
                        default:
                            return DependencyProperty.UnsetValue;
                    }
                }
                else if (targetType == typeof(FontWeight))
                {
                    switch (type)
                    {
                        case ServerTreeViewType.Subtag:
                        case ServerTreeViewType.Group:
                            return FontWeights.Bold;
                        default:
                            return DependencyProperty.UnsetValue;
                    }
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
