using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Shadowsocks.Util
{
    public static class ViewUtils
    {
        public static void BringToFront(this FrameworkElement element)
        {
            if (element?.Parent is Panel parent)
            {
                var maxZ = parent.Children.OfType<UIElement>()
                        .Where(x => x != element)
                        .Select(Panel.GetZIndex)
                        .Max();
                Panel.SetZIndex(element, maxZ + 1);
            }
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    var child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T dependencyObject)
                    {
                        yield return dependencyObject;
                    }

                    foreach (var childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }
}
