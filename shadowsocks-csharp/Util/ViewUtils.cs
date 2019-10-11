using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Drawing.Color;
using Size = System.Drawing.Size;

namespace Shadowsocks.Util
{
    public static class ViewUtils
    {
        private static readonly Color ColorMaskDirect = Color.FromArgb(255, 102, 102, 255);
        private static readonly Color ColorMaskPac = Color.FromArgb(102, 204, 102);
        private static readonly Color ColorMaskGlobal = Color.FromArgb(255, 102, 255, 255);

        [DllImport(@"user32.dll", CharSet = CharSet.Auto)]
        public static extern bool DestroyIcon(IntPtr handle);

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

        public static Color SelectColorMask(bool isProxyEnabled, bool isGlobalProxy)
        {
            if (isProxyEnabled)
            {
                return isGlobalProxy ? ColorMaskGlobal : ColorMaskPac;
            }

            return ColorMaskDirect;
        }

        public static Bitmap ChangeBitmapColor(Bitmap original, Color colorMask, bool isRandom = false)
        {
            var newBitmap = new Bitmap(original);

            for (var x = 0; x < newBitmap.Width; ++x)
            {
                for (var y = 0; y < newBitmap.Height; ++y)
                {
                    var color = original.GetPixel(x, y);
                    if (color.A != 0)
                    {
                        var red = Convert.ToByte(color.R * colorMask.R * (isRandom ? 2.5 : 1) / 255);
                        var green = Convert.ToByte(color.G * colorMask.G / 255);
                        var blue = Convert.ToByte(color.B * colorMask.B / 255);
                        var alpha = Convert.ToByte(color.A * colorMask.A / 255);
                        newBitmap.SetPixel(x, y, Color.FromArgb(alpha, red, green, blue));
                    }
                    else
                    {
                        newBitmap.SetPixel(x, y, color);
                    }
                }
            }
            return newBitmap;
        }

        public static Bitmap ResizeBitmap(Bitmap original, int width, int height)
        {
            var newBitmap = new Bitmap(width, height);
            using (var g = Graphics.FromImage(newBitmap))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(original, new Rectangle(0, 0, width, height));
            }
            return newBitmap;
        }

        public static int GetDpi()
        {
            var dpiXProperty = typeof(SystemParameters).GetProperty(@"DpiX", BindingFlags.NonPublic | BindingFlags.Static);
            if (dpiXProperty != null)
            {
                var dpiX = (int)dpiXProperty.GetValue(null, null);
                return dpiX;
            }
            return 96;
        }

        /// <summary>
        /// Determine the icon size based on the screen DPI.
        /// </summary>
        /// <returns></returns>
        /// https://stackoverflow.com/a/40851713/2075611
        public static Size GetIconSize()
        {
            Size size;
            var dpi = GetDpi();
            if (dpi < 97)
            {
                // dpi = 96;//100%
                size = new Size(16, 16);
            }
            else if (dpi < 121)
            {
                // dpi = 120;//125%
                size = new Size(20, 20);
            }
            else if (dpi < 145)
            {
                // dpi = 144;//150%
                size = new Size(24, 24);
            }
            else if (dpi < 169)
            {
                // dpi = 168;//175%
                size = new Size(28, 28);
            }
            else
            {
                // dpi = 192;//200%
                size = new Size(32, 32);
            }
            return size;
        }

        public static void SetResource(ResourceDictionary resources, string filename, int index)
        {
            var url = new Uri(filename, UriKind.Relative);
            if (resources.MergedDictionaries.Count > index)
            {
                resources.MergedDictionaries[index].Source = url;
            }
            else if (Application.LoadComponent(url) is ResourceDictionary langRd)
            {
                resources.MergedDictionaries.Add(langRd);
            }
        }

        public static bool IsOnScreen(Window window)
        {
            return IsOnScreen(window.Left, window.Top)
                || IsOnScreen(window.Left + window.Width, window.Top + window.Height);
        }

        public static bool IsOnScreen(double x, double y)
        {
            return
                    SystemParameters.VirtualScreenLeft <= x &&
                    SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth >= x &&
                    SystemParameters.VirtualScreenTop <= y &&
                    SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight >= y;
        }

        public static bool IsScrolledToEnd(this TextBox textBox)
        {
            return Math.Abs(textBox.VerticalOffset + textBox.ViewportHeight - textBox.ExtentHeight) < 0.001;
        }
    }
}