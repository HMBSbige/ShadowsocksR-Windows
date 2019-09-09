using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Shadowsocks.Controller;
using WpfColorFontDialog;

namespace Shadowsocks.View
{
    public partial class LogWindow
    {
        public LogWindow()
        {
            InitializeComponent();
            LoadLanguage();
        }

        private const int MaxReadSize = 65536;

        private string _currentLogFile;
        private string _currentLogFileName;
        private long _currentOffset;

        private void LoadLanguage()
        {
            Title = I18N.GetString(@"Log Viewer");
            FileMenuItem.Header = I18N.GetString(@"_File");
            ClearLogMenuItem.Header = I18N.GetString(@"Clear _log");
            ShowInExplorerMenuItem.Header = I18N.GetString(@"Show in _Explorer");
            CloseMenuItem.Header = I18N.GetString(@"_Close");
            ViewMenuItem.Header = I18N.GetString(@"_View");
            FontMenuItem.Header = I18N.GetString(@"_Font...");
            WrapTextMenuItem.Header = I18N.GetString(@"_Wrap Text");
            AlwaysOnTopMenuItem.Header = I18N.GetString(@"_Always on top");
        }

        private void ReadLog()
        {
            var newLogFile = Logging.LogFile;
            if (newLogFile != _currentLogFile)
            {
                _currentOffset = 0;
                _currentLogFile = newLogFile;
                _currentLogFileName = Logging.LogFileName;
            }

            try
            {
                using var reader = new StreamReader(new FileStream(newLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                if (_currentOffset == 0)
                {
                    var maxSize = reader.BaseStream.Length;
                    if (maxSize > MaxReadSize)
                    {
                        reader.BaseStream.Seek(-MaxReadSize, SeekOrigin.End);
                        reader.ReadLine();
                    }
                }
                else
                {
                    reader.BaseStream.Seek(_currentOffset, SeekOrigin.Begin);
                }

                var txt = reader.ReadToEnd();
                if (!string.IsNullOrEmpty(txt))
                {
                    LogTextBox.AppendText(txt);
                    LogTextBox.ScrollToEnd();
                }

                _currentOffset = reader.BaseStream.Position;
            }
            catch (FileNotFoundException)
            {

            }
            catch (ArgumentNullException)
            {

            }

            Title = $@"{I18N.GetString(@"Log Viewer")} {_currentLogFileName}";
        }

        private void ClearLogMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Logging.Clear();
            _currentOffset = 0;
            LogTextBox.Clear();
        }

        private void ShowInExplorerMenuItem_Click(object sender, RoutedEventArgs _)
        {
            try
            {
                var argument = $@"/n,/select,{Logging.LogFile}";
                System.Diagnostics.Process.Start(@"explorer.exe", argument);
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SyncLog(_cts);
        }

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private async void SyncLog(CancellationTokenSource cts)
        {
            while (true)
            {
                ReadLog();
                try
                {
                    await Task.Delay(100, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                if (cts.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void FontMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ColorFontDialog
            {
                Font = FontInfo.GetControlFont(LogTextBox)
            };

            if (dialog.ShowDialog() == true)
            {
                var font = dialog.Font;
                if (font != null)
                {
                    FontInfo.ApplyFont(LogTextBox, font);
                }
            }
        }

        private void WrapTextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            WrapTextMenuItem.IsChecked = !WrapTextMenuItem.IsChecked;
        }

        private void AlwaysOnTopMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AlwaysOnTopMenuItem.IsChecked = !AlwaysOnTopMenuItem.IsChecked;
        }

        private void WrapTextMenuItem_Checked(object sender, RoutedEventArgs e)
        {
            LogTextBox.TextWrapping = WrapTextMenuItem.IsChecked ? TextWrapping.Wrap : TextWrapping.NoWrap;
            LogTextBox.ScrollToEnd();
        }

        private void AlwaysOnTopMenuItem_Checked(object sender, RoutedEventArgs e)
        {
            Topmost = AlwaysOnTopMenuItem.IsChecked;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _cts.Cancel();
        }
    }
}
