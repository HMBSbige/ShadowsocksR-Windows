using Shadowsocks.Util;
using System.Windows;

namespace Shadowsocks.View
{
    public partial class ImageWindow
    {
        public ImageWindow()
        {
            InitializeComponent();
            Title = I18NUtil.GetAppStringValue(@"Donate");
            Height += SystemParameters.WindowCaptionHeight;
        }
    }
}
