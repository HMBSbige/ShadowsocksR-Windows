using Shadowsocks.Model;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Shadowsocks.Controller
{
    public class ChnDomainsAndIPUpdater
    {
        private const string CNIP_URL = @"https://ftp.apnic.net/apnic/stats/apnic/delegated-apnic-latest";
        private const string CNDOMAINS_URL = @"https://raw.githubusercontent.com/felixonmars/dnsmasq-china-list/master/accelerated-domains.china.conf";
        private const string SS_CNIP_TEMPLATE_URL = @"https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/ss_cnip_temp.pac";
        private const string SS_WHITE_TEMPLATE_URL = @"https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/ss_white_temp.pac";
        private const string SS_WHITER_TEMPLATE_URL = @"https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/ss_white_r_temp.pac";
        private string TEMPLATE_URL = null;

        private const string USER_AGENT = @"Mozilla/5.0 (Windows NT 5.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/35.0.3319.102 Safari/537.36";

        private static readonly string PAC_FILE = PACServer.PAC_FILE;
        private static readonly string USER_RULE_FILE = PACServer.WHITELIST_FILE;
        private static readonly string USER_TEMPLATE_FILE = PACServer.USER_WHITELIST_TEMPLATE_FILE;

        private static string SS_template = null;
        private static string cnIpRange = null;
        private static string cnIp16Range = null;

        private Configuration lastConfig = null;
        private Templates lastTemplate = Templates.None;

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

        public enum Templates
        {
            None,
            ss_cnip,
            ss_white,
            ss_white_r
        }

        #region private

        private void HttpDownloadSSCNIPTemplateCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                string result;
                if (File.Exists(USER_TEMPLATE_FILE))
                {
                    result = File.ReadAllText(USER_TEMPLATE_FILE, Encoding.UTF8);
                    if (result.IndexOf(@"__cnIpRange__", StringComparison.Ordinal) > 0
                        && result.IndexOf(@"__cnIp16Range__", StringComparison.Ordinal) > 0
                        && result.IndexOf(@"__white_domains__", StringComparison.Ordinal) > 0
                        && result.IndexOf(@"FindProxyForURL", StringComparison.Ordinal) > 0)
                    {
                        SS_template = result;
                        if (lastConfig != null && lastTemplate != Templates.None)
                        {
                            UpdatePACFromChnDomainsAndIP(lastConfig, lastTemplate);
                        }
                        return;
                    }
                }
                result = e.Result;
                if (result.IndexOf(@"__cnIpRange__", StringComparison.Ordinal) > 0
                    && result.IndexOf(@"__cnIp16Range__", StringComparison.Ordinal) > 0
                    && result.IndexOf(@"__white_domains__", StringComparison.Ordinal) > 0
                    && result.IndexOf(@"FindProxyForURL", StringComparison.Ordinal) > 0)
                {
                    SS_template = result;
                    if (lastConfig != null && lastTemplate != Templates.None)
                    {
                        UpdatePACFromChnDomainsAndIP(lastConfig, lastTemplate);
                    }
                }
                else
                {
                    Error?.Invoke(this, new ErrorEventArgs(new Exception(@"WhiteList Template ERROR")));
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        private void HttpDownloadCNIPCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                var ipv4Subnets = GetCNIP.ReadFromString(e.Result);
                if (ipv4Subnets == null)
                {
                    Error?.Invoke(this, new ErrorEventArgs(new Exception(@"Empty CNIP")));
                }
                else
                {
                    cnIpRange = GetCNIP.GetcnIpRange(ipv4Subnets);
                    cnIp16Range = GetCNIP.GetcnIp16Range(ipv4Subnets);
                    if (lastConfig != null && lastTemplate != Templates.None)
                    {
                        UpdatePACFromChnDomainsAndIP(lastConfig, lastTemplate);
                    }
                }

            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        private void HttpDownloadDomainsCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                if (!(GetCNDomains.ReadFromString(e.Result) is HashSet<string> domains))
                {
                    Error?.Invoke(this, new ErrorEventArgs(new Exception(@"Empty CNDomains")));
                }
                else
                {
                    if (File.Exists(USER_RULE_FILE))
                    {
                        var local = File.ReadAllText(USER_RULE_FILE, Encoding.UTF8);
                        var rules = local.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var domain in rules)
                        {
                            if (!string.IsNullOrWhiteSpace(domain))
                            {
                                domains.Add(domain);
                            }
                        }
                    }

                    var result = SS_template
                            .Replace(@"__cnIpRange__", cnIpRange)
                            .Replace(@"__cnIp16Range__", cnIp16Range)
                            .Replace(@"__white_domains__", GetCNDomains.GetPACwhitedomains(domains));
                    if (File.Exists(PAC_FILE))
                    {
                        var original = File.ReadAllText(PAC_FILE, Encoding.UTF8);
                        if (original == result)
                        {
                            UpdateCompleted?.Invoke(this, new ResultEventArgs(false));
                            return;
                        }
                    }

                    File.WriteAllText(PAC_FILE, result, Encoding.UTF8);
                    UpdateCompleted?.Invoke(this, new ResultEventArgs(true));
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
            finally
            {
                lastConfig = null;
                lastTemplate = Templates.None;
                SS_template = null;
                cnIpRange = null;
                cnIp16Range = null;
            }
        }

        #endregion

        #region public

        public void UpdatePACFromChnDomainsAndIP(Configuration config, Templates template)
        {
            if (SS_template == null)
            {
                lastConfig = config;
                lastTemplate = template;
                var http = new WebClient();
                http.Headers.Add(@"User-Agent", string.IsNullOrEmpty(config.proxyUserAgent) ? USER_AGENT : config.proxyUserAgent);
                var proxy = new WebProxy(IPAddress.Loopback.ToString(), config.localPort);
                if (!string.IsNullOrEmpty(config.authPass))
                {
                    proxy.Credentials = new NetworkCredential(config.authUser, config.authPass);
                }
                http.Proxy = proxy;
                http.DownloadStringCompleted += HttpDownloadSSCNIPTemplateCompleted;
                switch (template)
                {
                    case Templates.ss_white:
                        TEMPLATE_URL = SS_WHITE_TEMPLATE_URL;
                        break;
                    case Templates.ss_white_r:
                        TEMPLATE_URL = SS_WHITER_TEMPLATE_URL;
                        break;
                    case Templates.ss_cnip:
                        TEMPLATE_URL = SS_CNIP_TEMPLATE_URL;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(template), template, null);
                }
                http.DownloadStringAsync(new Uri(TEMPLATE_URL + @"?rnd=" + Utils.RandUInt32()));
            }
            else if (cnIpRange == null || cnIp16Range == null)
            {
                var http = new WebClient();
                http.Headers.Add(@"User-Agent", string.IsNullOrEmpty(config.proxyUserAgent) ? USER_AGENT : config.proxyUserAgent);
                var proxy = new WebProxy(IPAddress.Loopback.ToString(), config.localPort);
                if (!string.IsNullOrEmpty(config.authPass))
                {
                    proxy.Credentials = new NetworkCredential(config.authUser, config.authPass);
                }
                http.Proxy = proxy;
                http.DownloadStringCompleted += HttpDownloadCNIPCompleted;
                http.DownloadStringAsync(new Uri(CNIP_URL + @"?rnd=" + Utils.RandUInt32()));
            }
            else
            {
                var http = new WebClient();
                http.Headers.Add(@"User-Agent", string.IsNullOrEmpty(config.proxyUserAgent) ? USER_AGENT : config.proxyUserAgent);
                var proxy = new WebProxy(IPAddress.Loopback.ToString(), config.localPort);
                if (!string.IsNullOrEmpty(config.authPass))
                {
                    proxy.Credentials = new NetworkCredential(config.authUser, config.authPass);
                }
                http.Proxy = proxy;
                http.DownloadStringCompleted += HttpDownloadDomainsCompleted;
                http.DownloadStringAsync(new Uri(CNDOMAINS_URL + @"?rnd=" + Utils.RandUInt32()));
            }
        }

        #endregion
    }
}