using Shadowsocks.Model;
using Shadowsocks.Util;
using Shadowsocks.Util.GitHubRelease;
using System;
using System.Collections.Generic;

namespace Shadowsocks.Controller.HttpRequest
{
    public class UpdateChecker : HttpRequest
    {
        private const string Owner = @"HMBSbige";
        private const string Repo = @"ShadowsocksR-Windows";

        public string LatestVersionNumber;
        public string LatestVersionUrl;

        public bool Found;

        public event EventHandler NewVersionFound;
        public event EventHandler NewVersionFoundFailed;
        public event EventHandler NewVersionNotFound;

        public const string Name = @"ShadowsocksR";
        public const string Copyright = @"Copyright © HMBSbige 2019 & BreakWa11 2017. Fork from Shadowsocks by clowwindy";
        public const string Version = @"5.1.8.2";

        public const string FullVersion = Version +
#if IsDotNetCore
        @" .Net Core" +
#else
        @"" +
#endif
#if IsSelfContainedDotNetCore
#if Is64Bit
            @" x64" +
#else
            @"" +
#endif
#endif
#if DEBUG
        @" Debug";
#else
        @"";
#endif

        public async void Check(Configuration config, bool notifyNoFound)
        {
            try
            {
                var updater = new GitHubRelease(Owner, Repo);
                var url = updater.AllReleaseUrl;
                var userAgent = config.ProxyUserAgent;
                var proxy = CreateProxy(config);

                var json = await AutoGetAsync(url, proxy, userAgent, config.ConnectTimeout * 1000);

                var releases = JsonUtils.Deserialize<List<Release>>(json);
                var latestRelease = VersionUtil.GetLatestRelease(releases, config.IsPreRelease);
                if (VersionUtil.CompareVersion(latestRelease.tag_name, Version) > 0)
                {
                    LatestVersionNumber = latestRelease.tag_name;
                    LatestVersionUrl = latestRelease.html_url;
                    Found = true;
                    NewVersionFound?.Invoke(this, new EventArgs());
                }
                else
                {
                    LatestVersionNumber = latestRelease.tag_name;
                    if (notifyNoFound)
                    {
                        NewVersionNotFound?.Invoke(this, new EventArgs());
                    }
                }
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
                if (notifyNoFound)
                {
                    NewVersionFoundFailed?.Invoke(this, new EventArgs());
                }
            }
        }
    }
}
