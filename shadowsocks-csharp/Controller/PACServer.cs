using Shadowsocks.Model;
using Shadowsocks.Properties;
using Shadowsocks.Util;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Shadowsocks.Controller
{
    public class PACServer : Listener.Service
    {
        public static string gfwlist_FILE = @"gfwlist.txt";

        public static string PAC_FILE = @"pac.txt";

        public static string USER_RULE_FILE = @"user-rule.txt";

        public static string USER_ABP_FILE = @"abp.txt";

        public static string WHITELIST_FILE = @"whitelist.txt";

        public static string USER_WHITELIST_TEMPLATE_FILE = @"user_whitelist_temp.txt";

        public string PacUrl { get; private set; } = "";

        FileSystemWatcher PACFileWatcher;
        FileSystemWatcher UserRuleFileWatcher;
        private Configuration _config;

        public event EventHandler PACFileChanged;
        public event EventHandler UserRuleFileChanged;

        public PACServer()
        {
            WatchPacFile();
            WatchUserRuleFile();
        }

        public void UpdateConfiguration(Configuration config)
        {
            _config = config;
            PacUrl = $@"http://127.0.0.1:{config.localPort}/pac?auth={config.localAuthPassword}&t={Utils.GetTimestamp(DateTime.Now)}";
        }

        public bool Handle(byte[] firstPacket, int length, Socket socket)
        {
            if (socket.ProtocolType != ProtocolType.Tcp)
            {
                return false;
            }
            try
            {
                var request = Encoding.UTF8.GetString(firstPacket, 0, length);
                var lines = request.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                bool hostMatch = false, pathMatch = false;
                var socksType = 0;
                string proxy = null;
                foreach (var line in lines)
                {
                    var kv = line.Split(new[] { ':' }, 2);
                    if (kv.Length == 2)
                    {
                        if (kv[0] == "Host")
                        {
                            if (kv[1].Trim() == ((IPEndPoint)socket.LocalEndPoint).ToString())
                            {
                                hostMatch = true;
                            }
                        }
                    }
                    else if (kv.Length == 1)
                    {
                        if (!Utils.isLocal(socket) || line.IndexOf($@"auth={_config.localAuthPassword}", StringComparison.Ordinal) > 0)
                        {
                            if (line.IndexOf(" /pac?", StringComparison.Ordinal) > 0 && line.IndexOf("GET", StringComparison.Ordinal) == 0)
                            {
                                var url = line.Substring(line.IndexOf(" ", StringComparison.Ordinal) + 1);
                                url = url.Substring(0, url.IndexOf(" ", StringComparison.Ordinal));
                                pathMatch = true;
                                var port_pos = url.IndexOf("port=", StringComparison.Ordinal);
                                if (port_pos > 0)
                                {
                                    var port = url.Substring(port_pos + 5);
                                    if (port.IndexOf("&", StringComparison.Ordinal) >= 0)
                                    {
                                        port = port.Substring(0, port.IndexOf("&", StringComparison.Ordinal));
                                    }

                                    var ip_pos = url.IndexOf("ip=", StringComparison.Ordinal);
                                    if (ip_pos > 0)
                                    {
                                        proxy = url.Substring(ip_pos + 3);
                                        if (proxy.IndexOf("&", StringComparison.Ordinal) >= 0)
                                        {
                                            proxy = proxy.Substring(0, proxy.IndexOf("&", StringComparison.Ordinal));
                                        }
                                        proxy += $@":{port};";
                                    }
                                    else
                                    {
                                        proxy = $@"127.0.0.1:{port};";
                                    }
                                }

                                if (url.IndexOf("type=socks4", StringComparison.Ordinal) > 0 || url.IndexOf("type=s4", StringComparison.Ordinal) > 0)
                                {
                                    socksType = 4;
                                }
                                if (url.IndexOf("type=socks5", StringComparison.Ordinal) > 0 || url.IndexOf("type=s5", StringComparison.Ordinal) > 0)
                                {
                                    socksType = 5;
                                }
                            }
                        }
                    }
                }
                if (hostMatch && pathMatch)
                {
                    SendResponse(socket, socksType, proxy);
                    return true;
                }
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static string TouchPACFile()
        {
            if (File.Exists(PAC_FILE))
            {
                return PAC_FILE;
            }
            else
            {
                FileManager.DecompressFile(PAC_FILE, Resources.proxy_pac_txt);
                return PAC_FILE;
            }
        }

        public static string TouchUserRuleFile()
        {
            if (File.Exists(USER_RULE_FILE))
            {
                return USER_RULE_FILE;
            }
            else
            {
                File.WriteAllText(USER_RULE_FILE, Resources.user_rule);
                return USER_RULE_FILE;
            }
        }

        private string GetPACContent()
        {
            if (File.Exists(PAC_FILE))
            {
                return File.ReadAllText(PAC_FILE, Encoding.UTF8);
            }
            else
            {
                return Utils.UnGzip(Resources.proxy_pac_txt);
            }
        }

        public void SendResponse(Socket socket, int socksType, string setProxy)
        {
            try
            {
                var pac = GetPACContent();

                var localEndPoint = (IPEndPoint)socket.LocalEndPoint;

                var proxy =
                    setProxy == null ? GetPACAddress(localEndPoint, socksType) :
                    socksType == 5 ? $@"SOCKS5 {setProxy}" :
                    socksType == 4 ? $@"SOCKS {setProxy}" :
                    $@"PROXY {setProxy}";

                if (_config.pacDirectGoProxy && _config.proxyEnable)
                {
                    if (_config.proxyType == 0)
                    {
                        pac = pac.Replace(@"__DIRECT__", $@"SOCKS5 {_config.proxyHost}:{_config.proxyPort};DIRECT;");
                    }
                    else if (_config.proxyType == 1)
                    {
                        pac = pac.Replace(@"__DIRECT__", $@"PROXY {_config.proxyHost}:{_config.proxyPort};DIRECT;");
                    }
                }
                else
                {
                    pac = pac.Replace(@"__DIRECT__", @"DIRECT;");
                }

                pac = pac.Replace(@"__PROXY__", $@"{proxy}DIRECT;");

                var text = $@"HTTP/1.1 200 OK
Server: ShadowsocksR
Content-Type: application/x-ns-proxy-autoconfig
Content-Length: {Encoding.UTF8.GetBytes(pac).Length}
Connection: Close

{pac}";
                var response = Encoding.UTF8.GetBytes(text);
                socket.BeginSend(response, 0, response.Length, 0, SendCallback, socket);
                Utils.ReleaseMemory();
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
        }

        private static void SendCallback(IAsyncResult ar)
        {
            var conn = (Socket)ar.AsyncState;
            try
            {
                conn.Shutdown(SocketShutdown.Both);
                conn.Close();
            }
            catch
            {
                // ignored
            }
        }

        private void WatchPacFile()
        {
            PACFileWatcher?.Dispose();
            PACFileWatcher = new FileSystemWatcher(Directory.GetCurrentDirectory())
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = PAC_FILE
            };
            PACFileWatcher.Changed += PACFileWatcher_Changed;
            PACFileWatcher.Created += PACFileWatcher_Changed;
            PACFileWatcher.Deleted += PACFileWatcher_Changed;
            PACFileWatcher.Renamed += PACFileWatcher_Changed;
            PACFileWatcher.EnableRaisingEvents = true;
        }

        private void WatchUserRuleFile()
        {
            UserRuleFileWatcher?.Dispose();
            UserRuleFileWatcher = new FileSystemWatcher(Directory.GetCurrentDirectory())
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = USER_RULE_FILE
            };
            UserRuleFileWatcher.Changed += UserRuleFileWatcher_Changed;
            UserRuleFileWatcher.Created += UserRuleFileWatcher_Changed;
            UserRuleFileWatcher.Deleted += UserRuleFileWatcher_Changed;
            UserRuleFileWatcher.Renamed += UserRuleFileWatcher_Changed;
            UserRuleFileWatcher.EnableRaisingEvents = true;
        }

        #region FileSystemWatcher.OnChanged()
        // FileSystemWatcher Changed event is raised twice
        // http://stackoverflow.com/questions/1764809/filesystemwatcher-changed-event-is-raised-twice
        // Add a short delay to avoid raise event twice in a short period
        private void PACFileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (PACFileChanged != null)
            {
                Logging.Info($@"Detected: PAC file '{e.Name}' was {e.ChangeType.ToString().ToLower()}.");
                Task.Run(() =>
                {
                    ((FileSystemWatcher)sender).EnableRaisingEvents = false;
                    Task.Delay(10).Wait();
                    PACFileChanged(this, new EventArgs());
                    ((FileSystemWatcher)sender).EnableRaisingEvents = true;
                });
            }
        }

        private void UserRuleFileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (UserRuleFileChanged != null)
            {
                Logging.Info($@"Detected: User Rule file '{e.Name}' was {e.ChangeType.ToString().ToLower()}.");
                Task.Run(() =>
                {
                    ((FileSystemWatcher)sender).EnableRaisingEvents = false;
                    Task.Delay(10).Wait();
                    UserRuleFileChanged(this, new EventArgs());
                    ((FileSystemWatcher)sender).EnableRaisingEvents = true;
                });
            }
        }

        #endregion

        private string GetPACAddress(IPEndPoint localEndPoint, int socksType)
        {
            if (socksType == 5)
            {
                return $@"SOCKS5 {localEndPoint.Address}:{_config.localPort};";
            }
            if (socksType == 4)
            {
                return $@"SOCKS {localEndPoint.Address}:{_config.localPort};";
            }
            return $@"PROXY {localEndPoint.Address}:{_config.localPort};";
        }
    }
}
