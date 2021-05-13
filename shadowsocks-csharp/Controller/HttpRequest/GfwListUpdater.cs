using Shadowsocks.Controller.Service;
using Shadowsocks.Enums;
using Shadowsocks.Model;
using Shadowsocks.Properties;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Shadowsocks.Controller.HttpRequest
{
    public class GfwListUpdater : HttpRequest
    {
        private const string GfwlistUrl = @"https://raw.githubusercontent.com/gfwlist/gfwlist/master/gfwlist.txt";

        #region event

        public event EventHandler<ResultEventArgs> UpdateCompleted;

        public event ErrorEventHandler Error;

        public class ResultEventArgs : EventArgs
        {
            public readonly bool Success;
            public readonly PacType PacType;

            public ResultEventArgs(bool success, PacType pacType)
            {
                Success = success;
                PacType = pacType;
            }
        }

        #endregion

        #region GfwList

        public async void UpdatePacFromGfwList(Configuration config)
        {
            Logging.Info($@"Checking GFWList from {GfwlistUrl}");
            try
            {
                var userAgent = config.ProxyUserAgent;
                var proxy = CreateProxy(config);

                var content = await AutoGetAsync(GfwlistUrl, proxy, userAgent, config.ConnectTimeout * 1000, TimeSpan.FromMinutes(1).TotalMilliseconds);
                File.WriteAllText(Utils.GetTempPath(PACServer.gfwlist_FILE), content, Encoding.UTF8);
                var pacFileChanged = MergeAndWritePacFile(content);
                UpdateCompleted?.Invoke(this, new ResultEventArgs(pacFileChanged, PacType.GfwList));
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        public static bool MergeAndWritePacFile(string gfwListResult)
        {
            var abpContent = MergePacFile(gfwListResult);
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

        private static string MergePacFile(string gfwListResult)
        {
            var abpContent = File.Exists(PACDaemon.USER_ABP_FILE) ? FileManager.NonExclusiveReadAllText(PACDaemon.USER_ABP_FILE) : Resources.abp;

            var userRuleLines = new List<string>();
            if (File.Exists(PACDaemon.USER_RULE_FILE))
            {
                var userRulesString = FileManager.NonExclusiveReadAllText(PACDaemon.USER_RULE_FILE);
                userRuleLines = ParseToValidList(userRulesString);
            }

            var gfwLines = ParseBase64ToValidList(gfwListResult);

            abpContent = abpContent.Replace(@"__USERRULES__", JsonUtils.Serialize(userRuleLines, false))
                    .Replace(@"__RULES__", JsonUtils.Serialize(gfwLines, false));
            return abpContent;
        }

        private static List<string> ParseBase64ToValidList(string response)
        {
            var bytes = Convert.FromBase64String(response);
            var content = Encoding.ASCII.GetString(bytes);
            return ParseToValidList(content);
        }

        private static List<string> ParseToValidList(string content)
        {
            var lines = content.GetLines().ToArray();
            var validLines = new List<string>(lines.Length);
            validLines.AddRange(lines.Where(line => !line.StartsWith(@"!") && !line.StartsWith(@"[")));
            return validLines;
        }

        #endregion

        #region OnlinePAC

        public async void UpdateOnlinePac(Configuration config, string url)
        {
            try
            {
                var userAgent = config.ProxyUserAgent;
                var proxy = CreateProxy(config);

                var content = await AutoGetAsync(url, proxy, userAgent, config.ConnectTimeout * 1000, TimeSpan.FromMinutes(1).TotalMilliseconds);
                if (File.Exists(PACDaemon.PAC_FILE))
                {
                    var original = FileManager.NonExclusiveReadAllText(PACDaemon.PAC_FILE);
                    if (original == content)
                    {
                        UpdateCompleted?.Invoke(this, new ResultEventArgs(false, PacType.Online));
                        return;
                    }
                }

                File.WriteAllText(PACDaemon.PAC_FILE, content);
                UpdateCompleted?.Invoke(this, new ResultEventArgs(true, PacType.Online));
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        #endregion

    }
}
