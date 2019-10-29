﻿using Microsoft.Win32;
using Shadowsocks.Controller;
using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Model;
using Shadowsocks.Util;
using Shadowsocks.Util.SingleInstance;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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

            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            app.Exit += App_Exit;

            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(@"##SyncfusionLicense##");

            I18NUtil.SetLanguage(Configuration.Load().LangName);
            ViewUtils.SetResource(app.Resources, @"../View/NotifyIconResources.xaml", 1);

            _controller = new ShadowsocksController();

            // Logging
            Logging.DefaultOut = Console.Out;
            Logging.DefaultError = Console.Error;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls12;

            _viewController = new MenuViewController(_controller);
            SystemEvents.SessionEnding += _viewController.Quit_Click;

            _controller.Reload();
            if (_controller.IsDefaultConfig())
            {
                var res = MessageBox.Show(
                $@"{I18NUtil.GetAppStringValue(@"DefaultConfigMessage")}{Environment.NewLine}{I18NUtil.GetAppStringValue(@"DefaultConfigQuestion")}",
                UpdateChecker.Name, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.OK);
                switch (res)
                {
                    case MessageBoxResult.Yes:
                    {
                        _controller.ShowConfigForm();
                        break;
                    }
                    case MessageBoxResult.No:
                    {
                        _controller.ShowSubscribeWindow();
                        break;
                    }
                    default:
                    {
                        StopController();
                        return;
                    }
                }
            }
#if !DEBUG
            Reg.SetUrlProtocol(@"ssr");
            Reg.SetUrlProtocol(@"sub");
#endif
            singleInstance.ListenForArgumentsFromSuccessiveInstances();
            app.Run();
        }

        private static void StopController()
        {
            _viewController?.Quit_Click(default, default);
            _controller?.Stop();
            _controller = null;
        }

        private static void App_Exit(object sender, ExitEventArgs e)
        {
#if !DEBUG
            Reg.RemoveUrlProtocol(@"ssr");
            Reg.RemoveUrlProtocol(@"sub");
#endif
            StopController();
        }

        private static void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Resume:
                {
                    Logging.Info("os wake up");
                    if (_controller != null)
                    {
                        Task.Run(() =>
                        {
                            Thread.Sleep(10 * 1000);
                            try
                            {
                                _controller.Reload();
                                Logging.Info("controller started");
                            }
                            catch (Exception ex)
                            {
                                Logging.LogUsefulException(ex);
                            }
                        });
                    }
                    break;
                }
                case PowerModes.Suspend:
                {
                    if (_controller != null)
                    {
                        _controller.Stop();
                        Logging.Info("controller stopped");
                    }
                    Logging.Info("os suspend");
                    break;
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
                $@"{I18NUtil.GetAppStringValue(@"UnexpectedError")}{Environment.NewLine}{e.ExceptionObject}",
                    UpdateChecker.Name, MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private static void SingleInstance_ArgumentsReceived(object sender, ArgumentsReceivedEventArgs e)
        {
            if (e.Args.Contains(Constants.ParameterMultiplyInstance))
            {
                MessageBox.Show(I18NUtil.GetAppStringValue(@"SuccessiveInstancesMessage1") + Environment.NewLine +
                                I18NUtil.GetAppStringValue(@"SuccessiveInstancesMessage2"),
                        I18NUtil.GetAppStringValue(@"SuccessiveInstancesCaption"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            Application.Current.Dispatcher?.Invoke(() =>
            {
                _viewController.ImportAddress(string.Join(Environment.NewLine, e.Args));
            });
        }
    }
}
