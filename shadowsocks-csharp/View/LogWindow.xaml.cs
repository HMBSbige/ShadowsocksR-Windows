using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Shadowsocks.Controller;

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
            #region Get current font

            var style = LogTextBox.FontWeight == FontWeights.Bold
                    ? System.Drawing.FontStyle.Bold
                    : System.Drawing.FontStyle.Regular;
            if (LogTextBox.FontStyle == FontStyles.Italic)
            {
                style |= System.Drawing.FontStyle.Italic;
            }

            var isUnderline = true;
            foreach (var td in TextDecorations.Underline)
            {
                if (!LogTextBox.TextDecorations.Contains(td))
                {
                    isUnderline = false;
                    break;
                }
            }
            if (isUnderline)
            {
                style |= System.Drawing.FontStyle.Underline;
            }

            var isStrikeout = true;
            foreach (var td in TextDecorations.Strikethrough)
            {
                if (!LogTextBox.TextDecorations.Contains(td))
                {
                    isStrikeout = false;
                    break;
                }
            }
            if (isStrikeout)
            {
                style |= System.Drawing.FontStyle.Strikeout;
            }

            var name = @"Courier New";
            var familyNames = LogTextBox.FontFamily.FamilyNames.Values;
            var names = familyNames.ToArray();
            if (names.Length > 0)
            {
                name = names[0];
            }

            var font = new System.Drawing.Font(name, Convert.ToSingle(LogTextBox.FontSize * 72.0 / 96.0), style, System.Drawing.GraphicsUnit.Point, 0);

            #endregion

            using var fontDialog = new System.Windows.Forms.FontDialog
            {
                Font = font
            };
            if (fontDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LogTextBox.FontFamily = new FontFamily(fontDialog.Font.Name);
                LogTextBox.FontSize = fontDialog.Font.Size * 96.0 / 72.0;
                LogTextBox.FontWeight = fontDialog.Font.Bold ? FontWeights.Bold : FontWeights.Regular;
                LogTextBox.FontStyle = fontDialog.Font.Italic ? FontStyles.Italic : FontStyles.Normal;

                var tdc = new TextDecorationCollection();
                if (fontDialog.Font.Underline) tdc.Add(TextDecorations.Underline);
                if (fontDialog.Font.Strikeout) tdc.Add(TextDecorations.Strikethrough);
                LogTextBox.TextDecorations = tdc;
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
