using System;
using System.Dynamic;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Shadowsocks.Model;

namespace Shadowsocks.Controller
{
    public class UpdateChecker
    {
        private const string UpdateUrl = @"https://api.github.com/repos/HMBSbige/ShadowsocksR-Windows/releases/latest";

        private const string UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/74.0.3729.131 Safari/537.36";

        public string LatestVersionNumber;
        public string LatestVersionUrl;
        public bool Found;
        public event EventHandler NewVersionFound;
        public event EventHandler NewVersionNotFound;

        public const string Name = @"ShadowsocksR";
        public const string Copyright = @"Copyright © HMBSbige 2019 & BreakWa11 2017. Fork from Shadowsocks by clowwindy";
        public const string Version = @"5.0.1.2";

        public const string FullVersion = Version +
#if IsDotNetCore
        @" .Net Core" +
#else
        @"" +
#endif
#if IsSelfContainedDotNetCore
        @" SelfContained" +
#if Is64Bit
            @" x64" +
#else
            @"" +
#endif
#else
        @"" +
#endif
#if DEBUG
        @" Debug";
#else
        "";
#endif

        public static bool UseProxy = true;

        public void CheckUpdate(Configuration config, bool notifyNoFound = true)
        {
            try
            {
                var http = new WebClient { Encoding = Encoding.UTF8 };
                http.Headers.Add(@"User-Agent", string.IsNullOrEmpty(config.proxyUserAgent) ? UserAgent : config.proxyUserAgent);
                if (UseProxy)
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

                if (notifyNoFound)
                {
                    http.DownloadStringCompleted += http_DownloadStringCompleted;
                }
                else
                {
                    http.DownloadStringCompleted += http_DownloadStringCompleted2;
                }
                http.DownloadStringAsync(new Uri($@"{UpdateUrl}?rnd={Util.Utils.RandUInt32()}"));
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
            }
        }

        public static int CompareVersion(string l, string r)
        {
            var ls = l.Split('.');
            var rs = r.Split('.');
            for (var i = 0; i < Math.Max(ls.Length, rs.Length); i++)
            {
                var lp = i < ls.Length ? int.Parse(ls[i]) : 0;
                var rp = i < rs.Length ? int.Parse(rs[i]) : 0;
                if (lp != rp)
                {
                    return lp - rp;
                }
            }
            return 0;
        }

        private static bool IsNewVersion(string version)
        {
            return CompareVersion(version, Version) > 0;
        }

        private void CheckUpdate(string response, bool notifyNoFound)
        {
            string url = null, version = null;

            dynamic result = JsonConvert.DeserializeObject<ExpandoObject>(response);
            if (result.html_url is string)
            {
                if (result.tag_name is string)
                {
                    url = result.html_url;
                    version = result.tag_name;
                }
            }

            if (url == null || version == null || !IsNewVersion(version))
            {
                if (notifyNoFound)
                {
                    NewVersionNotFound?.Invoke(this, new EventArgs());
                }

                return;
            }

            Found = true;
            LatestVersionUrl = url;
            LatestVersionNumber = version;
            NewVersionFound?.Invoke(this, new EventArgs());
        }

        private void http_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null)
                {
                    Logging.LogUsefulException(e.Error);
                    return;
                }
                CheckUpdate(e.Result, true);
            }
            catch (Exception ex)
            {
                Logging.LogUsefulException(ex);
            }
        }

        private void http_DownloadStringCompleted2(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null)
                {
                    Logging.LogUsefulException(e.Error);
                    return;
                }
                CheckUpdate(e.Result, false);
            }
            catch (Exception ex)
            {
                Logging.LogUsefulException(ex);
            }
        }
    }
}
