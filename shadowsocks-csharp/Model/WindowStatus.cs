using Shadowsocks.Util;
using System.Windows;

namespace Shadowsocks.Model
{
    public class WindowStatus
    {
        public double Width;
        public double Height;
        public double Left;
        public double Top;
        public WindowState State;

        public WindowStatus()
        {
            Width = 0.0;
            Height = 0.0;
            Left = 0.0;
            Top = 0.0;
            State = WindowState.Normal;
        }

        public WindowStatus(Window window)
        {
            Width = window.Width;
            Height = window.Height;
            Left = window.Left;
            Top = window.Top;
            State = window.WindowState;
        }

        public void SetStatus(Window window)
        {
            window.Width = Width;
            window.Height = Height;
            window.Left = Left;
            window.Top = Top;
            window.WindowState = State;
            window.WindowStartupLocation = ViewUtils.IsOnScreen(window) ? WindowStartupLocation.Manual : WindowStartupLocation.CenterScreen;
        }
    }
}
