using System;
using System.Windows;
using System.Windows.Controls;

namespace Shadowsocks.View.Controls
{
    public partial class NumberUpDown
    {
        private int _numValue;

        private int _maxNum = 65535;

        private int _minNum;

        public int NumValue
        {
            get
            {
                if (_numValue > _maxNum)
                {
                    return _maxNum;
                }
                if (_numValue < _minNum)
                {
                    return _minNum;
                }
                return _numValue;
            }
            set
            {
                if (_numValue != value)
                {
                    _numValue = value;
                    TxtNum.Text = value.ToString();
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string Value
        {
            get => GetValue(ValueProperty) as string;
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(@"Value", typeof(string), typeof(NumberUpDown));

        public int MinNum
        {
            get => _minNum;
            set => _minNum = value > _maxNum ? _maxNum : value;
        }

        public int MaxNum
        {
            get => _maxNum;
            set => _maxNum = value < _minNum ? _minNum : value;
        }

        public event EventHandler ValueChanged;

        public NumberUpDown()
        {
            InitializeComponent();
        }

        private void Up_Click(object sender, RoutedEventArgs e)
        {
            if (NumValue < _maxNum)
            {
                ++NumValue;
            }
            else
            {
                NumValue = _maxNum;
            }
        }

        private void Down_Click(object sender, RoutedEventArgs e)
        {
            if (NumValue > _minNum)
            {
                --NumValue;
            }
            else
            {
                NumValue = _minNum;
            }
        }

        private void TxtNum_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtNum == null)
            {
                return;
            }

            if (int.TryParse(TxtNum.Text, out var num))
            {
                NumValue = num;
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            TxtNum.Text = NumValue.ToString();
        }

        private void Grid_LostFocus(object sender, RoutedEventArgs e)
        {
            TxtNum.Text = NumValue.ToString();
        }
    }
}
