using Shadowsocks.Properties;
using System;
using System.Drawing;
using System.Windows.Forms;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace Shadowsocks.View
{
    public sealed partial class ShowTextForm : Form
    {
        public ShowTextForm(string title, string text)
        {
            Icon = Icon.FromHandle(Resources.ssw128.GetHicon());
            InitializeComponent();

            Text = title;
            PictureQRcode.Height = ClientSize.Height - textBox.Height;

            _options = new QrCodeEncodingOptions
            {
                DisableECI = true,
                CharacterSet = @"UTF-8",
                Margin = 0
            };

            GenQr(string.IsNullOrEmpty(text) ? string.Empty : text);
            textBox.Text = text;
        }

        private readonly EncodingOptions _options;

        private void GenQr(string str)
        {
            try
            {
                var code = Encoder.encode(str, ErrorCorrectionLevel.H, _options.Hints);
                var m = code.Matrix;
                var blockSize = Math.Max(PictureQRcode.Height / (m.Height + 1), 1);

                var qrWidth = m.Width * blockSize;
                var qrHeight = m.Height * blockSize;
                var dWidth = PictureQRcode.Width - qrWidth;
                var dHeight = PictureQRcode.Height - qrHeight;
                var maxD = Math.Max(dWidth, dHeight);
                PictureQRcode.SizeMode = maxD >= 7 * blockSize ? PictureBoxSizeMode.Zoom : PictureBoxSizeMode.CenterImage;

                var drawArea = new Bitmap(m.Width * blockSize, m.Height * blockSize);
                using (var g = Graphics.FromImage(drawArea))
                {
                    g.Clear(Color.White);
                    using (Brush b = new SolidBrush(Color.Black))
                    {
                        for (var row = 0; row < m.Width; ++row)
                        {
                            for (var col = 0; col < m.Height; ++col)
                            {
                                if (m[row, col] != 0)
                                {
                                    g.FillRectangle(b, blockSize * row, blockSize * col, blockSize, blockSize);
                                }
                            }
                        }
                    }
                }

                PictureQRcode.Image = drawArea;

            }
            catch
            {
                // ignored
            }
        }

        private void textBox_TextChanged(object sender, EventArgs e)
        {
            GenQr(textBox.Text);
        }

        private void ShowTextForm_SizeChanged(object sender, EventArgs e)
        {
            PictureQRcode.Height = ClientSize.Height - textBox.Height;
            GenQr(textBox.Text);
        }

        private void textBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Use KeyPress to avoid the beep when press Ctrl + A, don't do it in KeyDown
            if (e.KeyChar == '\x1')
            {
                textBox.SelectAll();
                e.Handled = true;
            }
        }
    }
}
