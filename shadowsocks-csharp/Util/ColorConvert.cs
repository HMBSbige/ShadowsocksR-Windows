using System;
using System.Windows.Media;
using Color = System.Drawing.Color;

namespace Shadowsocks.Util
{
    public static class ColorConvert
    {
        private static readonly Color[] SpeedColorList = { Color.White, Color.LightGreen, Color.Yellow, Color.Pink, Color.Red, Color.Red };
        private static readonly long[] SpeedBytesList = { 0, 1024 * 64, 1024 * 512, 1024 * 1024 * 4, 1024 * 1024 * 16, 1024 * 1024 * 1024 };

        private static readonly Color[] ConnectionColorList = { Color.White, Color.LightGreen, Color.Yellow, Color.Red, Color.Red };
        private static readonly long[] ConnectionBytesList = { 0, 16, 32, 64, 65536 };

        private static byte ColorMix(byte a, byte b, double alpha)
        {
            return (byte)(b * alpha + a * (1 - alpha));
        }

        private static System.Windows.Media.Color ColorMix(Color a, Color b, double alpha)
        {
            return System.Windows.Media.Color.FromRgb(ColorMix(a.R, b.R, alpha),
                    ColorMix(a.G, b.G, alpha),
                    ColorMix(a.B, b.B, alpha));
        }

        public static System.Windows.Media.Color GetSpeedColor(long bytes)
        {
            for (var i = 1; i < SpeedColorList.Length; ++i)
            {
                if (bytes < SpeedBytesList[i])
                {
                    var color = ColorMix(SpeedColorList[i - 1],
                            SpeedColorList[i],
                            (double)(bytes - SpeedBytesList[i - 1]) / (SpeedBytesList[i] - SpeedBytesList[i - 1]));
                    return color;
                }
            }
            return Colors.Transparent;
        }

        public static System.Windows.Media.Color GetConnectionColor(long connections)
        {
            for (var i = 1; i < ConnectionColorList.Length; ++i)
            {
                if (connections < ConnectionBytesList[i])
                {
                    var color = ColorMix(ConnectionColorList[i - 1],
                                    ConnectionColorList[i],
                                    (double)(connections - ConnectionBytesList[i - 1]) / (ConnectionBytesList[i] - ConnectionBytesList[i - 1]));
                    return color;
                }
            }
            return Colors.Transparent;
        }

        public static System.Windows.Media.Color GetConnectErrorColor(long val)
        {
            return System.Windows.Media.Color.FromRgb(255, (byte)Math.Max(0, 255 - val * 2.5), (byte)Math.Max(0, 255 - val * 2.5));
        }

        public static System.Windows.Media.Color GetConnectEmptyColor(long val)
        {
            return System.Windows.Media.Color.FromRgb(255, (byte)Math.Max(0, 255 - val * 8), (byte)Math.Max(0, 255 - val * 8));
        }

        public static System.Windows.Media.Color GetErrorPercentColor(double? percent)
        {
            if (percent != null)
            {
                return System.Windows.Media.Color.FromRgb(255, (byte)(255 - percent * 2), (byte)(255 - percent * 2));
            }
            return Colors.Transparent;
        }
    }
}
