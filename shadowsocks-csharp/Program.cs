using Shadowsocks.Controller;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Shadowsocks.Model;
using Shadowsocks.Util;
#if !_CONSOLE
using Shadowsocks.View;
#endif

namespace Shadowsocks
{
    static class Program
    {
        static ShadowsocksController _controller;
#if !_CONSOLE
        static MenuViewController _viewController;
#endif

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Utils.GetExecutablePath()));
#if !_CONSOLE
            foreach (var arg in args)
            {
                if (arg == "--setautorun")
                {
                    if (!AutoStartup.Switch())
                    {
                        Environment.ExitCode = 1;
                    }
                    return;
                }
            }
            using (var mutex = new Mutex(false, "Global\\ShadowsocksR_" + Directory.GetCurrentDirectory().GetDeterministicHashCode()))
            {
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                Application.EnableVisualStyles();
                Application.ApplicationExit += Application_ApplicationExit;
                SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
                Application.SetCompatibleTextRenderingDefault(false);

                if (!mutex.WaitOne(0, false))
                {
                    MessageBox.Show(I18N.GetString("Find Shadowsocks icon in your notify tray.") + Environment.NewLine +
                        I18N.GetString("If you want to start multiple Shadowsocks, make a copy in another directory."),
                        I18N.GetString("ShadowsocksR is already running."));
                    return;
                }
#endif

#if !_CONSOLE
                var tryTimes = 0;
                while (Configuration.Load() == null)
                {
                    if (tryTimes >= 5)
                        return;
                    using (var dlg = new InputPassword())
                    {
                        if (dlg.ShowDialog() == DialogResult.OK)
                            Configuration.SetPassword(dlg.password);
                        else
                            return;
                    }
                    tryTimes += 1;
                }
#endif

                _controller = new ShadowsocksController();
                HostMap.Instance().LoadHostFile();

                // Logging
                Logging.DefaultOut = Console.Out;
                Logging.DefaultError = Console.Error;

#if _DOTNET_4_0
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

#endif
#if !_CONSOLE
                _viewController = new MenuViewController(_controller);
                SystemEvents.SessionEnding += _viewController.Quit_Click;
#endif

                _controller.Start();

#if !_CONSOLE

                Application.Run();
            }
#else
            Console.ReadLine();
            _controller.Stop();
#endif
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

        private static void Application_ApplicationExit(object sender, EventArgs e)
        {
            _controller?.Stop();
            _controller = null;
        }

        private static int _exited;
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (Interlocked.Increment(ref _exited) == 1)
            {
                Logging.Log(LogLevel.Error, e.ExceptionObject != null ? e.ExceptionObject.ToString() : "");
                MessageBox.Show(I18N.GetString("Unexpected error, ShadowsocksR will exit.") +
                    Environment.NewLine + (e.ExceptionObject != null ? e.ExceptionObject.ToString() : ""),
                    "Shadowsocks Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }
        }
    }
}
