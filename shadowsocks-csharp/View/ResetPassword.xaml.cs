using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Model;
using Shadowsocks.Util;
using System.Windows;
using System.Windows.Input;

namespace Shadowsocks.View
{
    public partial class ResetPassword
    {
        public ResetPassword()
        {
            InitializeComponent();
            I18NUtil.SetLanguage(Resources, @"ResetPassword");
            OldPassword.Focus();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (NewPassword1.Password == NewPassword2.Password && Configuration.SetPasswordTry(OldPassword.Password))
            {
                var cfg = Configuration.Load();
                Configuration.SetPassword(NewPassword1.Password);
                Configuration.Save(cfg);
                Close();
            }
            else
            {
                MessageBox.Show(this.GetWindowStringValue(@"PasswordMatchError"), UpdateChecker.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (OldPassword.IsFocused)
                {
                    NewPassword1.Focus();
                }
                else if (NewPassword1.IsFocused)
                {
                    NewPassword2.Focus();
                }
                else
                {
                    Button_Click(this, new RoutedEventArgs());
                }
            }
        }
    }
}
