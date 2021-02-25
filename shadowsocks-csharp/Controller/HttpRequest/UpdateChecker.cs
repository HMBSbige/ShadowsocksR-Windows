using Shadowsocks.Model;
using System;
using UpdateChecker;

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
        public const string Copyright = @"Copyright © HMBSbige 2019 - 2020 & BreakWa11 2017. Fork from Shadowsocks by clowwindy";
        public const string Version = @"5.2.4";

        public const string FullVersion = Version +
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
                var updater = new GitHubReleasesUpdateChecker(
                    Owner,
                    Repo,
                    config.IsPreRelease,
                    Version);

                var userAgent = config.ProxyUserAgent;
                var proxy = CreateProxy(config);
                using var client = CreateClient(true, proxy, userAgent, config.ConnectTimeout * 1000);

                var res = await updater.CheckAsync(client, default);
                LatestVersionNumber = updater.LatestVersion;
                Found = res;
                if (Found)
                {
                    LatestVersionUrl = updater.LatestVersionUrl;
                    NewVersionFound?.Invoke(this, new EventArgs());
                }
                else
                {
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
