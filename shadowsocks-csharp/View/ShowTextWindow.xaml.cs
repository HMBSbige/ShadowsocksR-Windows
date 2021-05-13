using Shadowsocks.Util;
using System;
using System.Windows;

namespace Shadowsocks.View
{
    public partial class ShowTextWindow
    {
        public ShowTextWindow(string text)
        {
            InitializeComponent();
            _initText = text ?? string.Empty;
            Loaded += ShowTextWindow_Loaded;
        }

        private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            GenQr(TextBox.Text);
        }

        private void ShowTextWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            GenQr(TextBox.Text);
        }

        private readonly string _initText;

        private void ShowTextWindow_Loaded(object sender, RoutedEventArgs e)
        {
            GenQr(_initText);
            TextBox.Text = _initText;
            SizeChanged += ShowTextWindow_SizeChanged;
            TextBox.TextChanged += TextBox_TextChanged;
        }

        private void GenQr(string text)
        {
            try
            {
                var h = Convert.ToInt32(Grid1.RowDefinitions[1].ActualHeight);
                var w = Convert.ToInt32(Grid1.ActualWidth);
                PictureQrCode.Source = text != string.Empty ? QrCodeUtils.GenQrCode(text, w, h) : QrCodeUtils.GenQrCode2(text, Math.Min(w, h));
            }
            catch
            {
                PictureQrCode.Source = null;
            }
        }
    }
}
