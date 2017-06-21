using Shadowsocks.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Windows.Forms;

namespace Shadowsocks.Controller
{
    public class UpdateFreeNode
    {
        private const string UpdateURL = "https://raw.githubusercontent.com/breakwa11/breakwa11.github.io/master/free/freenodeplain.txt";

        public event EventHandler NewFreeNodeFound;
        public string FreeNodeResult;

        public const string Name = "ShadowsocksR";

        public void CheckUpdate(Configuration config, string URL, bool use_proxy)
        {
            FreeNodeResult = null;
            try
            {
                WebClient http = new WebClient();
                http.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 5.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/35.0.3319.102 Safari/537.36");
                http.QueryString["rnd"] = Util.Utils.RandUInt32().ToString();
                if (use_proxy)
                {
                    WebProxy proxy = new WebProxy(IPAddress.Loopback.ToString(), config.localPort);
                    if (!string.IsNullOrEmpty(config.authPass))
                    {
                        proxy.Credentials = new NetworkCredential(config.authUser, config.authPass);
                    }
                    http.Proxy = proxy;
                }
                else
                {
                    http.Proxy = null;
                }
                //UseProxy = !UseProxy;
                http.DownloadStringCompleted += http_DownloadStringCompleted;
                http.DownloadStringAsync(new Uri(URL != null ? URL : UpdateURL));
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
            }
        }

        private void http_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                string response = e.Result;
                FreeNodeResult = response;

                if (NewFreeNodeFound != null)
                {
                    NewFreeNodeFound(this, new EventArgs());
                }
            }
            catch (Exception ex)
            {
                if (e.Error != null)
                {
                    Logging.Debug(e.Error.ToString());
                }
                Logging.Debug(ex.ToString());
                if (NewFreeNodeFound != null)
                {
                    NewFreeNodeFound(this, new EventArgs());
                }
                return;
            }
        }
    }

    public class UpdateSubscribeManager
    {
        Configuration _config;
        List<ServerSubscribe> _serverSubscribes;
        UpdateFreeNode _updater;
        string _URL;
        bool _use_proxy;

        public void CreateTask(Configuration config, UpdateFreeNode updater, int index, bool use_proxy)
        {
            if (_config == null)
            {
                _config = config;
                _updater = updater;
                _use_proxy = use_proxy;
                if (index < 0)
                {
                    _serverSubscribes = new List<ServerSubscribe>();
                    for (int i = 0; i < config.serverSubscribes.Count; ++i)
                    {
                        _serverSubscribes.Add(config.serverSubscribes[i]);
                    }
                }
                else if (index < _config.serverSubscribes.Count)
                {
                    _serverSubscribes = new List<ServerSubscribe>();
                    _serverSubscribes.Add(config.serverSubscribes[index]);
                }
                Next();
            }
        }

        public bool Next()
        {
            if (_serverSubscribes.Count == 0)
            {
                _config = null;
                return false;
            }
            else
            {
                _URL = _serverSubscribes[0].URL;
                _updater.CheckUpdate(_config, _URL, _use_proxy);
                _serverSubscribes.RemoveAt(0);
                return true;
            }
        }

        public string URL
        {
            get
            {
                return _URL;
            }
        }
    }
}
