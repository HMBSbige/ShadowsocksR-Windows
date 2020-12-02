using Shadowsocks.Model;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Shadowsocks.Controller.HttpRequest
{
    public abstract class HttpRequest
    {
        private const string DefaultUserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.66 Safari/537.36";
        private const int DefaultGetTimeout = 30000;
        private const int DefaultHeadTimeout = 4000;

        public static HttpClient CreateClient(bool useProxy, IWebProxy proxy, string userAgent, double timeout)
        {
            var httpClientHandler = new SocketsHttpHandler
            {
                Proxy = proxy,
                UseProxy = useProxy
            };

            var client = new HttpClient(httpClientHandler)
            {
                Timeout = TimeSpan.FromMilliseconds(timeout),
                DefaultRequestVersion = HttpVersion.Version20
            };

            client.DefaultRequestHeaders.Add(@"User-Agent", string.IsNullOrWhiteSpace(userAgent) ? DefaultUserAgent : userAgent);
            return client;
        }

        private static async Task<string> GetAsync(string url, IWebProxy proxy, string userAgent = DefaultUserAgent, double timeout = DefaultGetTimeout, bool useProxy = true)
        {
            using var client = CreateClient(useProxy, proxy, userAgent, timeout);
            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private static async Task<bool> HeadAsync(string url, IWebProxy proxy, double timeout = DefaultHeadTimeout)
        {
            using var client = CreateClient(proxy != null, proxy, DefaultUserAgent, timeout);
            try
            {
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                return true;
            }
            catch
            {
                return false;
            }
        }

        protected static async Task<string> AutoGetAsync(string url, IWebProxy proxy, string userAgent = @"", double headTimeout = DefaultHeadTimeout, double getTimeout = DefaultGetTimeout)
        {
            string res = null;
            if (await HeadAsync(url, proxy, headTimeout))
            {
                try
                {
                    res = await ProxyGetAsync(url, proxy, userAgent, getTimeout);
                }
                catch
                {
                    res = null;
                }
            }
            if (res != null)
            {
                return res;
            }

            res = await DirectGetAsync(url, userAgent, getTimeout);
            return res;
        }

        protected static async Task<string> DirectGetAsync(string url, string userAgent = DefaultUserAgent, double getTimeout = DefaultGetTimeout)
        {
            Logging.Info($@"GET request directly: {url}");
            return await GetAsync(url, null, userAgent, getTimeout, false);
        }

        protected static async Task<string> ProxyGetAsync(string url, IWebProxy proxy, string userAgent = DefaultUserAgent, double timeout = DefaultGetTimeout)
        {
            Logging.Info($@"GET request by proxy: {url}");
            return await GetAsync(url, proxy, userAgent, timeout);
        }

        protected static async Task<string> DefaultGetAsync(string url, string userAgent = DefaultUserAgent, double getTimeout = DefaultGetTimeout)
        {
            Logging.Info($@"GET request by default: {url}");
            return await GetAsync(url, null, userAgent, getTimeout);
        }

        protected static IWebProxy CreateProxy(Configuration config)
        {
            var proxy = new WebProxy(Global.LocalHost, config.LocalPort);
            if (!string.IsNullOrEmpty(config.AuthPass))
            {
                proxy.Credentials = new NetworkCredential(config.AuthUser, config.AuthPass);
            }

            return proxy;
        }
    }
}
