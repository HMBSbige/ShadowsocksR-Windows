using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Shadowsocks.Controller.HttpRequest
{
    public abstract class HttpRequest
    {
        private const string DefaultUserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/77.0.3865.90 Safari/537.36";
        private const int DefaultGetTimeout = 30000;
        private const int DefaultHeadTimeout = 4000;

        private static async Task<string> GetAsync(string url, IWebProxy proxy, string userAgent = @"", double timeout = DefaultGetTimeout)
        {
            var httpClientHandler = new HttpClientHandler
            {
                Proxy = proxy,
                UseProxy = proxy != null
            };
            var httpClient = new HttpClient(httpClientHandler)
            {
                Timeout = TimeSpan.FromMilliseconds(timeout)
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
                Timeout = TimeSpan.FromMilliseconds(timeout)
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
                Logging.Info($@"GET request by proxy: {url}");
                try
                {
                    res = await GetAsync(url, proxy, userAgent, getTimeout);
                }
                catch
                {
                    res = null;
                }
            }
            if (res == null)
            {
                Logging.Info($@"GET request directly: {url}");
                res = await GetAsync(url, null, userAgent, getTimeout);
            }
            return res;
        }
    }
}
