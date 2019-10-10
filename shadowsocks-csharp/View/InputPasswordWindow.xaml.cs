using System.Windows.Input;

namespace Shadowsocks.View
{
    public partial class InputPasswordWindow
    {
        public InputPasswordWindow()
        {
            InitializeComponent();
        }

        public string Password { private set; get; }

        private void OkButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Password = PasswordBox.Password;
            DialogResult = true;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Password = PasswordBox.Password;
                DialogResult = true;
                Close();
            }
        }
    }
}
