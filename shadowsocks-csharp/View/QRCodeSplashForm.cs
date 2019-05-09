using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;


namespace Shadowsocks.View
{
    public sealed class QRCodeSplashForm : Form
    {
        public Rectangle TargetRect;

        public QRCodeSplashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            Load += QRCodeSplashForm_Load;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.White;
            ClientSize = new Size(1, 1);
            ControlBox = false;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "QRCodeSplashForm";
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
        }

        private Timer timer;
        private int flashStep;
        private const double FPS = 1.0 / 15 * 1000; // System.Windows.Forms.Timer resolution is 15ms
        private const double ANIMATION_TIME = 0.5;
        private const int ANIMATION_STEPS = (int)(ANIMATION_TIME * FPS);
        Stopwatch sw;
        int x;
        int y;
        int w;
        int h;
        Bitmap bitmap;
        Graphics g;
        Pen pen;
        SolidBrush brush;

        private void QRCodeSplashForm_Load(object sender, EventArgs e)
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            flashStep = 0;
            x = 0;
            y = 0;
            w = Width;
            h = Height;
            sw = Stopwatch.StartNew();
            timer = new Timer { Interval = (int)(ANIMATION_TIME * 1000 / ANIMATION_STEPS) };
            timer.Tick += timer_Tick;
            timer.Start();
            bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            g = Graphics.FromImage(bitmap);
            pen = new Pen(Color.Red, 3);
            brush = new SolidBrush(Color.FromArgb(30, Color.Red));
        }

        void timer_Tick(object sender, EventArgs e)
        {
            var percent = sw.ElapsedMilliseconds / 1000.0 / ANIMATION_TIME;
            if (percent < 1)
            {
                // ease out
                percent = 1 - Math.Pow((1 - percent), 4);
                x = (int)(TargetRect.X * percent);
                y = (int)(TargetRect.Y * percent);
                w = (int)(TargetRect.Width * percent + Size.Width * (1 - percent));
                h = (int)(TargetRect.Height * percent + Size.Height * (1 - percent));
                //codeRectView.Location = new Point(x, y);
                //codeRectView.Size = new Size(w, h);
                pen.Color = Color.FromArgb((int)(255 * percent), Color.Red);
                brush.Color = Color.FromArgb((int)(30 * percent), Color.Red);
                g.Clear(Color.Transparent);
                g.FillRectangle(brush, x, y, w, h);
                g.DrawRectangle(pen, x, y, w, h);
                SetBitmap(bitmap);
            }
            else
            {
                if (flashStep == 0)
                {
                    timer.Interval = 100;
                    g.Clear(Color.Transparent);
                    SetBitmap(bitmap);
                }
                else if (flashStep == 1)
                {
                    timer.Interval = 50;
                    g.FillRectangle(brush, x, y, w, h);
                    g.DrawRectangle(pen, x, y, w, h);
                    SetBitmap(bitmap);
                }
                else if (flashStep == 2)
                {
                    g.Clear(Color.Transparent);
                    SetBitmap(bitmap);
                }
                else if (flashStep == 3)
                {
                    g.FillRectangle(brush, x, y, w, h);
                    g.DrawRectangle(pen, x, y, w, h);
                    SetBitmap(bitmap);
                }
                else if (flashStep == 4)
                {
                    g.Clear(Color.Transparent);
                    SetBitmap(bitmap);
                }
                else if (flashStep == 5)
                {
                    g.FillRectangle(brush, x, y, w, h);
                    g.DrawRectangle(pen, x, y, w, h);
                    SetBitmap(bitmap);
                }
                else
                {
                    sw.Stop();
                    timer.Stop();
                    pen.Dispose();
                    brush.Dispose();
                    bitmap.Dispose();
                    Close();
                }
                ++flashStep;
            }
        }

        // PerPixelAlphaForm.cs
        // http://www.codeproject.com/Articles/1822/Per-Pixel-Alpha-Blend-in-C
        // Rui Lopes
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00080000; // This form has to have the WS_EX_LAYERED extended style
                return cp;
            }
        }

        public void SetBitmap(Bitmap bm)
        {
            SetBitmap(bm, 255);
        }

        /// <para>Changes the current bitmap with a custom opacity level.  Here is where all happens!</para>
        public void SetBitmap(Bitmap bm, byte opacity)
        {
            if (bm.PixelFormat != PixelFormat.Format32bppArgb)
                throw new ApplicationException("The bitmap must be 32ppp with alpha-channel.");

            // The idea of this is very simple,
            // 1. Create a compatible DC with screen;
            // 2. Select the bitmap with 32bpp with alpha-channel in the compatible DC;
            // 3. Call the UpdateLayeredWindow.

            var screenDc = Win32.GetDC(IntPtr.Zero);
            var memDc = Win32.CreateCompatibleDC(screenDc);
            var hBitmap = IntPtr.Zero;
            var oldBitmap = IntPtr.Zero;

            try
            {
                hBitmap = bm.GetHbitmap(Color.FromArgb(0));  // grab a GDI handle from this GDI+ bitmap
                oldBitmap = Win32.SelectObject(memDc, hBitmap);

                var size = new Win32.Size(bm.Width, bm.Height);
                var pointSource = new Win32.Point(0, 0);
                var topPos = new Win32.Point(Left, Top);
                var blend = new Win32.BLENDFUNCTION
                {
                    BlendOp = Win32.AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = opacity,
                    AlphaFormat = Win32.AC_SRC_ALPHA
                };

                Win32.UpdateLayeredWindow(Handle, screenDc, ref topPos, ref size, memDc, ref pointSource, 0, ref blend, Win32.ULW_ALPHA);
            }
            finally
            {
                Win32.ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    Win32.SelectObject(memDc, oldBitmap);
                    //Windows.DeleteObject(hBitmap); // The documentation says that we have to use the Windows.DeleteObject... but since there is no such method I use the normal DeleteObject from Win32 GDI and it's working fine without any resource leak.
                    Win32.DeleteObject(hBitmap);
                }
                Win32.DeleteDC(memDc);
            }
        }
    }


    // class that exposes needed win32 gdi functions.
    internal static class Win32
    {

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int x;
            public int y;

            public Point(int x, int y) { this.x = x; this.y = y; }
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct Size
        {
            public int cx;
            public int cy;

            public Size(int cx, int cy) { this.cx = cx; this.cy = cy; }
        }


        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }


        public const int ULW_COLORKEY = 0x00000001;
        public const int ULW_ALPHA = 0x00000002;
        public const int ULW_OPAQUE = 0x00000004;

        public const byte AC_SRC_OVER = 0x00;
        public const byte AC_SRC_ALPHA = 0x01;


        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern int UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern int DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern int DeleteObject(IntPtr hObject);
    }
}
