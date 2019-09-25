using Shadowsocks.Controller;
using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Model;
using System.Windows;
using System.Windows.Input;

namespace Shadowsocks.View
{
    public partial class ResetPassword
    {
        public ResetPassword()
        {
            InitializeComponent();
            LoadLanguage();
            OldPassword.Focus();
        }

        private void LoadLanguage()
        {
            Title = I18N.GetString(@"ResetPassword");
            OldPasswordLabel.Content = $@"{I18N.GetString(@"Old password")}{I18N.GetString(@": ")}";
            OldPassword.ToolTip = I18N.GetString(@"Old password");
            NewPassword1Label.Content = $@"{I18N.GetString(@"New password")}{I18N.GetString(@": ")}";
            NewPassword1.ToolTip = I18N.GetString(@"New password");
            NewPassword2Label.Content = $@"{I18N.GetString(@"Confirm new password")}{I18N.GetString(@": ")}";
            NewPassword2.ToolTip = I18N.GetString(@"Confirm new password");
            OkButton.Content = I18N.GetString(@"OK");
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
                MessageBox.Show(I18N.GetString(@"Password NOT match"), I18N.GetString(UpdateChecker.Name), MessageBoxButton.OK, MessageBoxImage.Error);
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
