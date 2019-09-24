using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Shadowsocks.Controller.HttpRequest
{
    public abstract class HttpRequest
    {
        private const string UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/77.0.3865.90 Safari/537.36";

        protected static async Task<string> Get(string url, IWebProxy proxy, string userAgent = @"", double timeout = 10000)
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
            request.Headers.Add(@"User-Agent", string.IsNullOrWhiteSpace(userAgent) ? UserAgent : userAgent);

            var response = await httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();
            var resultStr = await response.Content.ReadAsStringAsync();
            return resultStr;
        }
    }
}
