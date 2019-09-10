using Shadowsocks.Model;
using Shadowsocks.Properties;
using Shadowsocks.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Shadowsocks.Controller.Service
{
    public class HttpProxyRunner
    {
        private static readonly string UNIQUE_CONFIG_FILE;
        private static readonly Job PRIVOXY_JOB;
        private Process _process;
        private const string ExeNameNoExt = @"ShadowsocksR";
        private const string ExeName = @"ShadowsocksR.exe";

        static HttpProxyRunner()
        {
            try
            {
                var uid = Directory.GetCurrentDirectory().GetDeterministicHashCode();
                UNIQUE_CONFIG_FILE = $@"privoxy_{uid}.conf";
                PRIVOXY_JOB = new Job();

                FileManager.DecompressFile(Utils.GetTempPath(ExeName), Resources.privoxy_exe);
            }
            catch (IOException e)
            {
                Logging.LogUsefulException(e);
            }
        }

        public int RunningPort { get; private set; }

        public void Start(Configuration configuration)
        {
            if (_process == null)
            {
                var existingPrivoxy = Process.GetProcessesByName(ExeNameNoExt);
                foreach (var p in existingPrivoxy.Where(IsChildProcess))
                {
                    KillProcess(p);
                }
                var privoxyConfig = Resources.privoxy_conf;
                RunningPort = GetFreePort();
                privoxyConfig = privoxyConfig.Replace(@"__SOCKS_PORT__", configuration.localPort.ToString());
                privoxyConfig = privoxyConfig.Replace(@"__PRIVOXY_BIND_PORT__", RunningPort.ToString());
                privoxyConfig = privoxyConfig.Replace(@"__PRIVOXY_BIND_IP__",
                configuration.shareOverLan ? Configuration.AnyHost : Configuration.LocalHost)
                .Replace(@"__SOCKS_HOST__", Configuration.LocalHost);
                FileManager.ByteArrayToFile(Utils.GetTempPath(UNIQUE_CONFIG_FILE), Encoding.UTF8.GetBytes(privoxyConfig));

                _process = new Process
                {
                    // Configure the process using the StartInfo properties.
                    StartInfo =
                        {
                                FileName = ExeName,
                                Arguments = UNIQUE_CONFIG_FILE,
                                WorkingDirectory = Utils.GetTempPath(),
                                WindowStyle = ProcessWindowStyle.Hidden,
                                UseShellExecute = true,
                                CreateNoWindow = true
                        }
                };
                _process.Start();

                /*
                 * Add this process to job obj associated with this ss process, so that
                 * when ss exit unexpectedly, this process will be forced killed by system.
                 */
                PRIVOXY_JOB.AddProcess(_process.Handle);
            }
        }

        public void Stop()
        {
            if (_process != null)
            {
                KillProcess(_process);
                _process.Dispose();
                _process = null;
            }
        }

        private static void KillProcess(Process p)
        {
            try
            {
                p.CloseMainWindow();
                p.WaitForExit(100);
                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit();
                }
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
            }
        }

        private static bool IsChildProcess(Process process)
        {
            try
            {
                var path = process.MainModule?.FileName;

                return Utils.GetTempPath(ExeName).Equals(path);

            }
            catch (Exception ex)
            {
                /*
                 * Sometimes Process.GetProcessesByName will return some processes that
                 * are already dead, and that will cause exceptions here.
                 * We could simply ignore those exceptions.
                 */
                Logging.LogUsefulException(ex);
                return false;
            }
        }

        public static int GetFreePort()
        {
            const int defaultPort = 60000;
            try
            {
                // TCP stack please do me a favor
                var l = new TcpListener(GlobalConfiguration.OSSupportsLocalIPv6
                        ? IPAddress.IPv6Loopback
                        : IPAddress.Loopback, 0);
                l.Start();
                var port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                return port;
            }
            catch (Exception e)
            {
                // in case access denied
                Logging.LogUsefulException(e);
                return defaultPort;
            }
        }
    }
}
