using System;
using System.Windows;

namespace Shadowsocks.View.Controls
{
    public partial class MaskedTextBox
    {
        public MaskedTextBox()
        {
            InitializeComponent();
            PlainModeChanged += MaskedTextBoxPlainModeChanged;
            MyPasswordBox.PasswordChanged += (o, e) =>
            {
                if (MyPasswordBox.IsFocused)
                {
                    Text = MyPasswordBox.Password;
                }
            };
            MyTextBox.TextChanged += (o, e) =>
            {
                if (!MyPasswordBox.IsFocused)
                {
                    MyPasswordBox.Password = MyTextBox.Text;
                }
            };
        }

        private bool _plainMode;
        public bool PlainMode
        {
            get => _plainMode;
            set
            {
                if (_plainMode != value)
                {
                    _plainMode = value;
                    PlainModeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string Text
        {
            get => GetValue(TextProperty) as string;
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(@"Text", typeof(string), typeof(MaskedTextBox));

        public event EventHandler PlainModeChanged;

        private void MaskedTextBoxPlainModeChanged(object sender, EventArgs e)
        {
            if (_plainMode)
            {
                MyPasswordBox.Visibility = Visibility.Collapsed;
                MyTextBox.Visibility = Visibility.Visible;
            }
            else
            {
                MyPasswordBox.Visibility = Visibility.Visible;
                MyTextBox.Visibility = Visibility.Collapsed;
            }
        }
    }
}
