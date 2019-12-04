using Shadowsocks.Model;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Shadowsocks.Controller.HttpRequest
{
    public abstract class HttpRequest
    {
        private const string DefaultUserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/78.0.3904.97 Safari/537.36";
        private const int DefaultGetTimeout = 30000;
        private const int DefaultHeadTimeout = 4000;

        private static async Task<string> GetAsync(string url, IWebProxy proxy, string userAgent = @"", double timeout = DefaultGetTimeout, bool setProxy = true)
        {
            var httpClientHandler = new HttpClientHandler();
            if (setProxy)
            {
                httpClientHandler.Proxy = proxy;
                httpClientHandler.UseProxy = proxy != null;
            }
            var httpClient = new HttpClient(httpClientHandler)
            {
                Timeout = TimeSpan.FromMilliseconds(timeout),
#if IsDotNetCore
                DefaultRequestVersion = new Version(2, 0)
#endif
            };
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add(@"User-Agent", string.IsNullOrWhiteSpace(userAgent) ? DefaultUserAgent : userAgent);

            var response = await httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();
            var resultStr = await response.Content.ReadAsStringAsync();
            return resultStr;
        }

        private static async Task<bool> HeadAsync(string url, IWebProxy proxy, double timeout = DefaultHeadTimeout)
        {
            var httpClientHandler = new HttpClientHandler
            {
                Proxy = proxy,
                UseProxy = proxy != null
            };
            var httpClient = new HttpClient(httpClientHandler)
            {
                Timeout = TimeSpan.FromMilliseconds(timeout),
#if IsDotNetCore
                DefaultRequestVersion = new Version(2, 0)
#endif
            };

            HttpResponseMessage response = null;
            try
            {
                response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                response?.Dispose();
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
            if (res != null) return res;
            res = await DirectGetAsync(url, userAgent, getTimeout);
            return res;
        }

        protected static async Task<string> DirectGetAsync(string url, string userAgent = @"", double getTimeout = DefaultGetTimeout)
        {
            Logging.Info($@"GET request directly: {url}");
            return await GetAsync(url, null, userAgent, getTimeout);
        }

        protected static async Task<string> ProxyGetAsync(string url, IWebProxy proxy, string userAgent = @"", double timeout = DefaultGetTimeout)
        {
            Logging.Info($@"GET request by proxy: {url}");
            return await GetAsync(url, proxy, userAgent, timeout);
        }

        protected static async Task<string> DefaultGetAsync(string url, string userAgent = @"", double getTimeout = DefaultGetTimeout)
        {
            Logging.Info($@"GET request by default: {url}");
            return await GetAsync(url, null, userAgent, getTimeout, false);
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
