using System;
using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using ZXing.Windows.Compatibility;
using Brush = System.Drawing.Brush;
using Color = System.Drawing.Color;

namespace Shadowsocks.Util;

public static class QrCodeUtils
{
    private static BitmapImage ToBitmapImage(Image src)
    {
        var ms = new MemoryStream();
        src.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
        var image = new BitmapImage();
        image.BeginInit();
        ms.Seek(0, SeekOrigin.Begin);
        image.StreamSource = ms;
        image.EndInit();
        return image;
    }

    public static ImageSource GenQrCode(string content, int width, int height)
    {
        var options = new QrCodeEncodingOptions
        {
            DisableECI = true,
            CharacterSet = @"UTF-8",
            Width = width,
            Height = height,
            Margin = 0,
            ErrorCorrection = ErrorCorrectionLevel.H
        };
        var write = new BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = options
        };
        var bitmap = write.Write(content);
        return ToBitmapImage(bitmap);
    }

    public static ImageSource GenQrCode2(string str, int height)
    {
        var options = new QrCodeEncodingOptions
        {
            DisableECI = true,
            CharacterSet = @"UTF-8",
            Margin = 0
        };
        var code = Encoder.encode(str, ErrorCorrectionLevel.H, options.Hints);
        var m = code.Matrix;
        var blockSize = Math.Max(height / (m.Height + 1), 1);

        var drawArea = new Bitmap(m.Width * blockSize, m.Height * blockSize);
        using (var g = Graphics.FromImage(drawArea))
        {
            g.Clear(Color.White);
            using Brush b = new SolidBrush(Color.Black);
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

        return ToBitmapImage(drawArea);
    }

    public static Result ScanBitmap(Bitmap target)
    {
        var source = new BitmapLuminanceSource(target);
        var bitmap = new BinaryBitmap(new HybridBinarizer(source));
        var reader = new QRCodeReader();
        return reader.decode(bitmap);
    }
}
