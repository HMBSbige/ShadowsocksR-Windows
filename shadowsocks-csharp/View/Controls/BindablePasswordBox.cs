using System.Windows;
using System.Windows.Controls;

namespace Shadowsocks.View.Controls
{
    public sealed class BindablePasswordBox : Decorator
    {
        /// <summary>
        /// The password dependency property.
        /// </summary>
        public static readonly DependencyProperty PasswordProperty;

        private bool _isPreventCallback;
        private readonly RoutedEventHandler _savedCallback;

        /// <summary>
        /// Static constructor to initialize the dependency properties.
        /// </summary>
        static BindablePasswordBox()
        {
            PasswordProperty = DependencyProperty.Register(
                "Password",
                typeof(string),
                typeof(BindablePasswordBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordPropertyChanged)
            );
        }

        /// <summary>
        /// Saves the password changed callback and sets the child element to the password box.
        /// </summary>
        public BindablePasswordBox()
        {
            _savedCallback = HandlePasswordChanged;

            var passwordBox = new PasswordBox();
            passwordBox.PasswordChanged += _savedCallback;
            Child = passwordBox;
        }

        /// <summary>
        /// The password dependency property.
        /// </summary>
        public string Password
        {
            get => GetValue(PasswordProperty) as string;
            set => SetValue(PasswordProperty, value);
        }

        /// <summary>
        /// Handles changes to the password dependency property.
        /// </summary>
        /// <param name="d">the dependency object</param>
        /// <param name="eventArgs">the event args</param>
        private static void OnPasswordPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs eventArgs)
        {
            var bindablePasswordBox = (BindablePasswordBox)d;
            var passwordBox = (PasswordBox)bindablePasswordBox.Child;

            if (bindablePasswordBox._isPreventCallback)
            {
                return;
            }

            passwordBox.PasswordChanged -= bindablePasswordBox._savedCallback;
            passwordBox.Password = eventArgs.NewValue != null ? eventArgs.NewValue.ToString() : string.Empty;
            passwordBox.PasswordChanged += bindablePasswordBox._savedCallback;
        }

        /// <summary>
        /// Handles the password changed event.
        /// </summary>
        /// <param name="sender">the sender</param>
        /// <param name="eventArgs">the event args</param>
        private void HandlePasswordChanged(object sender, RoutedEventArgs eventArgs)
        {
            var passwordBox = (PasswordBox)sender;

            _isPreventCallback = true;
            Password = passwordBox.Password;
            _isPreventCallback = false;
        }
    }
}
