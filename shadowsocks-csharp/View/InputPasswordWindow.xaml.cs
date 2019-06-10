using Shadowsocks.Controller;
using System.Windows.Input;

namespace Shadowsocks.View
{
    public partial class InputPasswordWindow
    {
        public InputPasswordWindow()
        {
            InitializeComponent();
            Title = I18N.GetString(@"InputPassword");
            Label1.Content = I18N.GetString(@"Parse gui-config.json error, maybe require password to decrypt");
            OkButton.Content = I18N.GetString(@"OK");
        }

        public string Password { private set; get; }

        private void OkButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Password = PasswordBox1.Password;
            DialogResult = true;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Password = PasswordBox1.Password;
                DialogResult = true;
                Close();
            }
        }
    }
}
