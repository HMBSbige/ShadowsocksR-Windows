using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;

namespace Shadowsocks.View
{
    public partial class QRCodeSplashWindow
    {
        public QRCodeSplashWindow()
        {
            InitializeComponent();
            Loaded += Window_Loaded;
        }

        public Rectangle TargetRect;

        private int _flashStep;
        private const double Fps = 1.0 / 15.6 * 1000; // Timer resolution is 15.625ms
        private const double AnimationTime = 0.5;
        private const int AnimationSteps = (int)(AnimationTime * Fps);
        private double _x;
        private double _y;
        private double _w;
        private double _h;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Hidden;
            _flashStep = 0;
            _x = Left;
            _y = Top;
            _w = Width;
            _h = Height;
            Splash();
        }

        private async void Splash()
        {
            var sw = Stopwatch.StartNew();
            var interval = (int)(AnimationTime * 1000 / AnimationSteps);
            while (true)
            {
                var percent = sw.ElapsedMilliseconds / 1000.0 / AnimationTime;
                if (percent < 1)
                {
                    percent = 1 - Math.Pow(1 - percent, 4);
                    Left = _x + TargetRect.X * percent;
                    Top = _y + TargetRect.Y * percent;
                    Width = TargetRect.Width * percent + _w * (1 - percent);
                    Height = TargetRect.Height * percent + _h * (1 - percent);
                    Visibility = Visibility.Visible;
                }
                else
                {
                    if (_flashStep == 0)
                    {
                        interval = 100;
                        Visibility = Visibility.Hidden;
                    }
                    else if (_flashStep == 1)
                    {
                        interval = 50;
                        Visibility = Visibility.Visible;
                    }
                    else if (_flashStep == 2)
                    {
                        Visibility = Visibility.Hidden;
                    }
                    else if (_flashStep == 3)
                    {
                        Visibility = Visibility.Visible;
                    }
                    else if (_flashStep == 4)
                    {
                        Visibility = Visibility.Hidden;
                    }
                    else if (_flashStep == 5)
                    {
                        Visibility = Visibility.Visible;
                    }
                    else
                    {
                        sw.Stop();
                        Close();
                        return;
                    }
                    ++_flashStep;
                }
                await Task.Delay(interval);
            }
        }
    }
}
