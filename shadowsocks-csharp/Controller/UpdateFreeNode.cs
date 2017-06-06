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

        public void CheckUpdate(Configuration config, bool use_proxy)
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
                http.DownloadStringAsync(new Uri(config.nodeFeedURL != null ? config.nodeFeedURL : UpdateURL));
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
}
