using System;
using System.Linq;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace Shadowsocks.Util
{
    public static class ColorConvert
    {
        public static Color EnableColor = Color.FromRgb(0, 255, 170);
        public static readonly Brush EnableBrush = new SolidColorBrush(EnableColor);
        public static Color DisableColor = Colors.Red;
        public static readonly Brush DisableBrush = new SolidColorBrush(DisableColor);

        public static Color TotalDownloadColor1 = Color.FromRgb(229, 255, 246);
        public static readonly Brush TotalDownloadBrush1 = new SolidColorBrush(TotalDownloadColor1);
        public static Color TotalDownloadColor2 = Color.FromRgb(218, 242, 234);
        public static readonly Brush TotalDownloadBrush2 = new SolidColorBrush(TotalDownloadColor2);

        public static Color TotalUploadColor1 = Color.FromRgb(255, 229, 229);
        public static readonly Brush TotalUploadBrush1 = new SolidColorBrush(TotalUploadColor1);
        public static Color TotalUploadColor2 = Color.FromRgb(242, 218, 218);
        public static readonly Brush TotalUploadBrush2 = new SolidColorBrush(TotalUploadColor2);

        public static Color TotalDownloadRawColor1 = Color.FromRgb(255, 255, 229);
        public static readonly Brush TotalDownloadRawBrush1 = new SolidColorBrush(TotalDownloadRawColor1);
        public static Color TotalDownloadRawColor2 = Color.FromRgb(242, 242, 218);
        public static readonly Brush TotalDownloadRawBrush2 = new SolidColorBrush(TotalDownloadRawColor2);

        private static readonly Color[] SpeedColorList = { Colors.White, Colors.LightGreen, Colors.Yellow, Colors.Pink, Colors.Red };
        private static readonly long[] SpeedBytesList = { 0, 1024 * 64, 1024 * 512, 1024 * 1024 * 4, 1024 * 1024 * 16 };

        private static readonly Color[] ConnectionColorList = { Colors.White, Colors.LightGreen, Colors.Yellow, Colors.Red };
        private static readonly long[] ConnectionBytesList = { 0, 16, 32, 64 };

        private static readonly Color[] LatencyColorList = { Colors.White, Colors.LightGreen, Colors.Yellow, Colors.Red };
        private static readonly long[] LatencyList = { 0, 128, 256, 512 };

        private static byte ColorMix(byte a, byte b, double alpha)
        {
            return (byte)(b * alpha + a * (1 - alpha));
        }

        private static Color ColorMix(Color a, Color b, double alpha)
        {
            return Color.FromRgb(ColorMix(a.R, b.R, alpha),
                    ColorMix(a.G, b.G, alpha),
                    ColorMix(a.B, b.B, alpha));
        }

        public static Color GetSpeedColor(long bytes)
        {
            for (var i = 1; i < SpeedColorList.Length; ++i)
            {
                if (bytes < SpeedBytesList[i])
                {
                    return ColorMix(SpeedColorList[i - 1],
                                    SpeedColorList[i],
                                    (double)(bytes - SpeedBytesList[i - 1]) / (SpeedBytesList[i] - SpeedBytesList[i - 1]));
                }
            }
            return SpeedColorList.Last();
        }

        public static Color GetConnectionColor(long connections)
        {
            for (var i = 1; i < ConnectionColorList.Length; ++i)
            {
                if (connections < ConnectionBytesList[i])
                {
                    return ColorMix(ConnectionColorList[i - 1],
                                    ConnectionColorList[i],
                                    (double)(connections - ConnectionBytesList[i - 1]) / (ConnectionBytesList[i] - ConnectionBytesList[i - 1]));
                }
            }
            return ConnectionColorList.Last();
        }

        public static Color GetLatencyColor(long latency)
        {
            for (var i = 1; i < ConnectionColorList.Length; ++i)
            {
                if (latency < LatencyList[i])
                {
                    return ColorMix(LatencyColorList[i - 1],
                                    LatencyColorList[i],
                                    (double)(latency - LatencyList[i - 1]) / (LatencyList[i] - LatencyList[i - 1]));
                }
            }
            return ConnectionColorList.Last();
        }

        public static Color GetConnectErrorColor(long val)
        {
            return Color.FromRgb(255, (byte)Math.Max(0, 255 - val * 2.5), (byte)Math.Max(0, 255 - val * 2.5));
        }

        public static Color GetConnectEmptyColor(long val)
        {
            return Color.FromRgb(255, (byte)Math.Max(0, 255 - val * 8), (byte)Math.Max(0, 255 - val * 8));
        }

        public static Color GetErrorPercentColor(double? percent)
        {
            if (percent != null)
            {
                return Color.FromRgb(255, (byte)(255 - percent * 2), (byte)(255 - percent * 2));
            }
            return Colors.Transparent;
        }
    }
}
