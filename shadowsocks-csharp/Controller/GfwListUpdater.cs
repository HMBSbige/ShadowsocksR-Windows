﻿using Shadowsocks.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Shadowsocks.Controller
{
    public class GFWListUpdater
    {
        private const string GFWLIST_URL = "https://raw.githubusercontent.com/gfwlist/gfwlist/master/gfwlist.txt";

        private const string GFWLIST_BACKUP_URL = "https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/gfwlist.txt";

        private const string GFWLIST_TEMPLATE_URL = "https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/ss_gfw.pac";

        private const string USER_AGENT = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/74.0.3729.131 Safari/537.36";

        private static string PAC_FILE = PACServer.PAC_FILE;

        private static string USER_RULE_FILE = PACServer.USER_RULE_FILE;

        private static string USER_ABP_FILE = PACServer.USER_ABP_FILE;

        private static string gfwlist_template = null;

        private Configuration lastConfig;

        public int update_type;

        public event EventHandler<ResultEventArgs> UpdateCompleted;

        public event ErrorEventHandler Error;

        public class ResultEventArgs : EventArgs
        {
            public bool Success;

            public ResultEventArgs(bool success)
            {
                this.Success = success;
            }
        }

        private void http_DownloadGFWTemplateCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                string result = e.Result;
                if (result.IndexOf("__RULES__") > 0 && result.IndexOf("FindProxyForURL") > 0)
                {
                    gfwlist_template = result;
                    if (lastConfig != null)
                    {
                        UpdatePACFromGFWList(lastConfig);
                    }
                    lastConfig = null;
                }
                else
                {
                    Error(this, new ErrorEventArgs(new Exception("Download ERROR")));
                }
            }
            catch (Exception ex)
            {
                if (Error != null)
                {
                    Error(this, new ErrorEventArgs(ex));
                }
            }
        }

        private void http_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                List<string> lines = ParseResult(e.Result);
                if (lines.Count == 0)
                {
                    throw new Exception("Empty GFWList");
                }
                if (File.Exists(USER_RULE_FILE))
                {
                    string local = File.ReadAllText(USER_RULE_FILE, Encoding.UTF8);
                    string[] rules = local.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string rule in rules)
                    {
                        if (rule.StartsWith("!") || rule.StartsWith("["))
                            continue;
                        lines.Add(rule);
                    }
                }
                string abpContent = gfwlist_template;
                if (File.Exists(USER_ABP_FILE))
                {
                    abpContent = File.ReadAllText(USER_ABP_FILE, Encoding.UTF8);
                }
                else
                {
                    abpContent = gfwlist_template;
                }
                abpContent = abpContent.Replace("__RULES__", SimpleJson.SimpleJson.SerializeObject(lines));
                if (File.Exists(PAC_FILE))
                {
                    string original = File.ReadAllText(PAC_FILE, Encoding.UTF8);
                    if (original == abpContent)
                    {
                        update_type = 0;
                        UpdateCompleted(this, new ResultEventArgs(false));
                        return;
                    }
                }
                File.WriteAllText(PAC_FILE, abpContent, Encoding.UTF8);
                if (UpdateCompleted != null)
                {
                    update_type = 0;
                    UpdateCompleted(this, new ResultEventArgs(true));
                }
            }
            catch (Exception ex)
            {
                if (Error != null)
                {
                    WebClient http = sender as WebClient;
                    if (http.BaseAddress.StartsWith(GFWLIST_URL))
                    {
                        http.BaseAddress = GFWLIST_BACKUP_URL;
                        http.DownloadStringAsync(new Uri(GFWLIST_BACKUP_URL + "?rnd=" + Util.Utils.RandUInt32().ToString()));
                    }
                    else
                    {
                        if (e.Error != null)
                        {
                            Error(this, new ErrorEventArgs(e.Error));
                        }
                        else
                        {
                            Error(this, new ErrorEventArgs(ex));
                        }
                    }
                }
            }
        }

        private void http_DownloadPACCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                string content = e.Result;
                if (File.Exists(PAC_FILE))
                {
                    string original = File.ReadAllText(PAC_FILE, Encoding.UTF8);
                    if (original == content)
                    {
                        update_type = 1;
                        UpdateCompleted(this, new ResultEventArgs(false));
                        return;
                    }
                }
                File.WriteAllText(PAC_FILE, content, Encoding.UTF8);
                if (UpdateCompleted != null)
                {
                    update_type = 1;
                    UpdateCompleted(this, new ResultEventArgs(true));
                }
            }
            catch (Exception ex)
            {
                if (Error != null)
                {
                    Error(this, new ErrorEventArgs(ex));
                }
            }

        }

        public void UpdatePACFromGFWList(Configuration config)
        {
            if (gfwlist_template == null)
            {
                lastConfig = config;
                WebClient http = new WebClient();
                http.Headers.Add("User-Agent", String.IsNullOrEmpty(config.proxyUserAgent) ? USER_AGENT : config.proxyUserAgent);
                WebProxy proxy = new WebProxy(IPAddress.Loopback.ToString(), config.localPort);
                if (!string.IsNullOrEmpty(config.authPass))
                {
                    proxy.Credentials = new NetworkCredential(config.authUser, config.authPass);
                }
                http.Proxy = proxy;
                http.DownloadStringCompleted += http_DownloadGFWTemplateCompleted;
                http.DownloadStringAsync(new Uri(GFWLIST_TEMPLATE_URL + "?rnd=" + Util.Utils.RandUInt32().ToString()));
            }
            else
            {
                WebClient http = new WebClient();
                http.Headers.Add("User-Agent", String.IsNullOrEmpty(config.proxyUserAgent) ? USER_AGENT : config.proxyUserAgent);
                WebProxy proxy = new WebProxy(IPAddress.Loopback.ToString(), config.localPort);
                if (!string.IsNullOrEmpty(config.authPass))
                {
                    proxy.Credentials = new NetworkCredential(config.authUser, config.authPass);
                }
                http.Proxy = proxy;
                http.BaseAddress = GFWLIST_URL;
                http.DownloadStringCompleted += http_DownloadStringCompleted;
                http.DownloadStringAsync(new Uri(GFWLIST_URL + "?rnd=" + Util.Utils.RandUInt32().ToString()));
            }
        }

        public void UpdatePACFromGFWList(Configuration config, string url)
        {
            WebClient http = new WebClient();
            http.Headers.Add("User-Agent", String.IsNullOrEmpty(config.proxyUserAgent) ? USER_AGENT : config.proxyUserAgent);
            WebProxy proxy = new WebProxy(IPAddress.Loopback.ToString(), config.localPort);
            if (!string.IsNullOrEmpty(config.authPass))
            {
                proxy.Credentials = new NetworkCredential(config.authUser, config.authPass);
            }
            http.Proxy = proxy;
            http.DownloadStringCompleted += http_DownloadPACCompleted;
            http.DownloadStringAsync(new Uri(url + "?rnd=" + Util.Utils.RandUInt32().ToString()));
        }

        public List<string> ParseResult(string response)
        {
            byte[] bytes = Convert.FromBase64String(response);
            string content = Encoding.ASCII.GetString(bytes);
            string[] lines = content.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> valid_lines = new List<string>(lines.Length);
            foreach (string line in lines)
            {
                if (line.StartsWith("!") || line.StartsWith("["))
                    continue;
                valid_lines.Add(line);
            }
            return valid_lines;
        }
    }
}