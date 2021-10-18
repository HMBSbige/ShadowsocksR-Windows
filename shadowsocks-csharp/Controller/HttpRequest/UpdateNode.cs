using Shadowsocks.Enums;
using Shadowsocks.Model;
using System;
using System.Net;

namespace Shadowsocks.Controller.HttpRequest
{
    public class UpdateNode : HttpRequest
    {
        public const string DefaultUpdateUrl = @"https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/freenodeplain.txt";

        public event EventHandler NewFreeNodeFound;
        public string FreeNodeResult;
        public ServerSubscribe SubscribeTask;
        public bool Notify;

        public void CheckUpdate(Configuration config, ServerSubscribe subscribeTask, bool notify)
        {
            FreeNodeResult = null;
            Notify = notify;
            try
            {
                var proxy = CreateProxy(config);
                SubscribeTask = subscribeTask;
                var url = subscribeTask.Url ?? DefaultUpdateUrl;
                Update(subscribeTask.ProxyType, proxy, config.ConnectTimeout * 1000, url, config.ProxyUserAgent);
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
            }
        }

        private async void Update(HttpRequestProxyType proxyType, IWebProxy proxy, int timeout, string url, string userAgent)
        {
            try
            {
                FreeNodeResult = proxyType switch
                {
                    HttpRequestProxyType.Auto => await AutoGetAsync(url, proxy, userAgent, timeout),
                    HttpRequestProxyType.Direct => await DirectGetAsync(url, userAgent, timeout),
                    HttpRequestProxyType.Proxy => await ProxyGetAsync(url, proxy, userAgent, timeout),
                    HttpRequestProxyType.SystemSetting => await DefaultGetAsync(url, userAgent, timeout),
                    _ => await AutoGetAsync(url, proxy, userAgent, timeout)
                };
            }
            catch (Exception ex)
            {
                Logging.Debug(ex.ToString());
            }

            NewFreeNodeFound?.Invoke(this, EventArgs.Empty);
        }
    }
}
