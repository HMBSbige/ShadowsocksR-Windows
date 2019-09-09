using System;
using System.Net;
using Shadowsocks.Model;
using Shadowsocks.Util;

namespace Shadowsocks.Controller.Service
{
    public class UpdateFreeNode
    {
        public const string DefaultUpdateUrl = "https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/freenodeplain.txt";

        private const string UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/74.0.3729.131 Safari/537.36";

        public event EventHandler NewFreeNodeFound;
        public string FreeNodeResult;
        public ServerSubscribe SubscribeTask;
        public bool Notify;

        public void CheckUpdate(Configuration config, ServerSubscribe subscribeTask, bool useProxy, bool notify)
        {
            FreeNodeResult = null;
            Notify = notify;
            try
            {
                var http = new WebClient();
                http.Headers.Add(@"User-Agent", string.IsNullOrEmpty(config.proxyUserAgent) ? UserAgent : config.proxyUserAgent);
                http.QueryString[@"rnd"] = Utils.RandUInt32().ToString();
                if (useProxy)
                {
                    var proxy = new WebProxy(IPAddress.Loopback.ToString(), config.localPort);
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

                SubscribeTask = subscribeTask;
                var url = subscribeTask.Url;

                http.DownloadStringCompleted += http_DownloadStringCompleted;
                http.DownloadStringAsync(new Uri(url ?? DefaultUpdateUrl));
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
                var response = e.Result;
                FreeNodeResult = response;

                NewFreeNodeFound?.Invoke(this, new EventArgs());
            }
            catch (Exception ex)
            {
                if (e.Error != null)
                {
                    Logging.Debug(e.Error.ToString());
                }
                Logging.Debug(ex.ToString());
                NewFreeNodeFound?.Invoke(this, new EventArgs());
            }
        }
    }
}
