using Microsoft.Win32;
using Shadowsocks.Controller;
using Shadowsocks.Model;
using Shadowsocks.Util;
using Shadowsocks.Util.SingleInstance;
using Shadowsocks.View;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;

namespace Shadowsocks
{
    internal static class Program
    {
        private static ShadowsocksController _controller;
        private static MenuViewController _viewController;

        [STAThread]
        private static void Main(string[] args)
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Utils.GetExecutablePath()) ?? throw new InvalidOperationException());
            if (args.Contains(Constants.ParameterSetautorun))
            {
                if (!AutoStartup.Switch())
                {
                    Environment.ExitCode = 1;
                }
                return;
            }

            var identifier = $@"Global\{UpdateChecker.Name}_{Directory.GetCurrentDirectory().GetDeterministicHashCode()}";
            using var singleInstance = new SingleInstance(identifier);
            if (!singleInstance.IsFirstInstance)
            {
                singleInstance.PassArgumentsToFirstInstance(args.Length == 0
                        ? args.Append(Constants.ParameterMultiplyInstance)
                        : args);
                return;
            }
            singleInstance.ArgumentsReceived += SingleInstance_ArgumentsReceived;

            var app = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            I18NUtil.SetLanguage(app.Resources, @"App");
            ViewUtils.SetResource(app.Resources, @"../View/NotifyIconResources.xaml", 1);

            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            app.Exit += App_Exit;

            var tryTimes = 0;
            while (Configuration.Load() == null)
            {
                if (tryTimes >= 5)
                    return;
                var dlg = new InputPasswordWindow();
                if (dlg.ShowDialog() == true)
                {
                    Configuration.SetPassword(dlg.Password);
                }
                else
                {
                    return;
                }
                tryTimes += 1;
            }

            _controller = new ShadowsocksController();
            HostMap.Instance().LoadHostFile();

            // Logging
            Logging.DefaultOut = Console.Out;
            Logging.DefaultError = Console.Error;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            _viewController = new MenuViewController(_controller);
            SystemEvents.SessionEnding += _viewController.Quit_Click;

            _controller.Start();
            Reg.SetUrlProtocol();
            singleInstance.ListenForArgumentsFromSuccessiveInstances();
            app.Run();
        }

        private static void App_Exit(object sender, ExitEventArgs e)
        {
            _controller?.Stop();
            _controller = null;
        }

        private static void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Resume:
                    if (_controller != null)
                    {
                        var timer = new System.Timers.Timer(5 * 1000);
                        timer.Elapsed += Timer_Elapsed;
                        timer.AutoReset = false;
                        timer.Enabled = true;
                        timer.Start();
                    }
                    break;
                case PowerModes.Suspend:
                    _controller?.Stop();
                    break;
            }
        }

        private static void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                _controller?.Start();
            }
            catch (Exception ex)
            {
                Logging.LogUsefulException(ex);
            }
            finally
            {
                try
                {
                    var timer = (System.Timers.Timer)sender;
                    timer.Enabled = false;
                    timer.Stop();
                    timer.Dispose();
                }
                catch (Exception ex)
                {
                    Logging.LogUsefulException(ex);
                }
            }
        }

        private static int _exited;
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (Interlocked.Increment(ref _exited) == 1)
            {
                Logging.Log(LogLevel.Error, $@"{e.ExceptionObject}");
                MessageBox.Show(
                $@"{I18N.GetString(@"Unexpected error, ShadowsocksR will exit.")}{Environment.NewLine}{e.ExceptionObject}",
                    UpdateChecker.Name, MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private static void SingleInstance_ArgumentsReceived(object sender, ArgumentsReceivedEventArgs e)
        {
            if (e.Args.Contains(Constants.ParameterMultiplyInstance))
            {
                MessageBox.Show(I18N.GetString("Find Shadowsocks icon in your notify tray.") + Environment.NewLine +
                                I18N.GetString("If you want to start multiple Shadowsocks, make a copy in another directory."),
                        I18N.GetString("ShadowsocksR is already running."), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            Application.Current.Dispatcher?.Invoke(() =>
            {
                _viewController.ImportAddress(string.Join(Environment.NewLine, e.Args));
            });
        }
    }
}
