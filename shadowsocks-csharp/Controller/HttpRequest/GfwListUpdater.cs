using Newtonsoft.Json;
using Shadowsocks.Controller.Service;
using Shadowsocks.Model;
using Shadowsocks.Properties;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Shadowsocks.Controller.HttpRequest
{
    public class GFWListUpdater
    {
        private const string GFWLIST_URL = @"https://raw.githubusercontent.com/gfwlist/gfwlist/master/gfwlist.txt";

        private const string USER_AGENT = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/74.0.3729.131 Safari/537.36";

        public int UpdateType;

        public event EventHandler<ResultEventArgs> UpdateCompleted;

        public event ErrorEventHandler Error;

        public class ResultEventArgs : EventArgs
        {
            public readonly bool Success;

            public ResultEventArgs(bool success)
            {
                Success = success;
            }
        }

        private void http_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                File.WriteAllText(Utils.GetTempPath(PACServer.gfwlist_FILE), e.Result, Encoding.UTF8);
                var pacFileChanged = MergeAndWritePACFile(e.Result);
                UpdateType = 0;
                UpdateCompleted?.Invoke(this, new ResultEventArgs(pacFileChanged));
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        public static bool MergeAndWritePACFile(string gfwListResult)
        {
            var abpContent = MergePACFile(gfwListResult);
            if (File.Exists(PACDaemon.PAC_FILE))
            {
                var original = FileManager.NonExclusiveReadAllText(PACDaemon.PAC_FILE, Encoding.UTF8);
                if (original == abpContent)
                {
                    return false;
                }
            }

            File.WriteAllText(PACDaemon.PAC_FILE, abpContent, Encoding.UTF8);
            return true;
        }

        private static string MergePACFile(string gfwListResult)
        {
            var abpContent = File.Exists(PACDaemon.USER_ABP_FILE) ? FileManager.NonExclusiveReadAllText(PACDaemon.USER_ABP_FILE, Encoding.UTF8) : Resources.abp;

            var userRuleLines = new List<string>();
            if (File.Exists(PACDaemon.USER_RULE_FILE))
            {
                var userRulesString = FileManager.NonExclusiveReadAllText(PACDaemon.USER_RULE_FILE, Encoding.UTF8);
                userRuleLines = ParseToValidList(userRulesString);
            }

            var gfwLines = ParseBase64ToValidList(gfwListResult);

            abpContent = abpContent.Replace("__USERRULES__", JsonConvert.SerializeObject(userRuleLines, Formatting.Indented))
                    .Replace("__RULES__", JsonConvert.SerializeObject(gfwLines, Formatting.Indented));
            return abpContent;
        }

        public void UpdatePACFromGFWList(Configuration config)
        {
            Logging.Info($@"Checking GFWList from {GFWLIST_URL}");
            var http = new WebClient();
            http.Headers.Add("User-Agent", string.IsNullOrEmpty(config.proxyUserAgent) ? USER_AGENT : config.proxyUserAgent);
            var proxy = new WebProxy(Configuration.LocalHost, config.localPort);
            if (!string.IsNullOrEmpty(config.authPass))
            {
                proxy.Credentials = new NetworkCredential(config.authUser, config.authPass);
            }
            http.Proxy = proxy;
            http.BaseAddress = GFWLIST_URL;
            http.DownloadStringCompleted += http_DownloadStringCompleted;
            http.DownloadStringAsync(new Uri($@"{GFWLIST_URL}?rnd={Rng.RandUInt32()}"));
        }

        #region OnlinePAC

        private void http_DownloadOnlinePACCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                var content = e.Result;
                if (File.Exists(PACDaemon.PAC_FILE))
                {
                    var original = File.ReadAllText(PACDaemon.PAC_FILE, Encoding.UTF8);
                    if (original == content)
                    {
                        UpdateType = 1;
                        UpdateCompleted?.Invoke(this, new ResultEventArgs(false));
                        return;
                    }
                }

                File.WriteAllText(PACDaemon.PAC_FILE, content, Encoding.UTF8);
                if (UpdateCompleted != null)
                {
                    UpdateType = 1;
                    UpdateCompleted(this, new ResultEventArgs(true));
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        public void UpdateOnlinePAC(Configuration config, string url)
        {
            var http = new WebClient();
            http.Headers.Add(@"User-Agent", string.IsNullOrEmpty(config.proxyUserAgent) ? USER_AGENT : config.proxyUserAgent);
            var proxy = new WebProxy(Configuration.LocalHost, config.localPort);
            if (!string.IsNullOrEmpty(config.authPass))
            {
                proxy.Credentials = new NetworkCredential(config.authUser, config.authPass);
            }
            http.Proxy = proxy;
            http.DownloadStringCompleted += http_DownloadOnlinePACCompleted;
            http.DownloadStringAsync(new Uri($@"{url}?rnd={Rng.RandUInt32()}"));
        }

        #endregion

        private static List<string> ParseBase64ToValidList(string response)
        {
            var bytes = Convert.FromBase64String(response);
            var content = Encoding.ASCII.GetString(bytes);
            return ParseToValidList(content);
        }

        private static List<string> ParseToValidList(string content)
        {
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var validLines = new List<string>(lines.Length);
            validLines.AddRange(lines.Where(line => !line.StartsWith("!") && !line.StartsWith("[")));
            return validLines;
        }
    }
}