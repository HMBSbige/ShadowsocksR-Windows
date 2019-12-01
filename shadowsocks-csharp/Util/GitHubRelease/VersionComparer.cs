using System.Collections.Generic;

namespace Shadowsocks.Util.GitHubRelease
{
    public class VersionComparer : IComparer<object>
    {
        public int Compare(object x, object y)
        {
            return VersionUtil.CompareVersion(x?.ToString(), y?.ToString());
        }
    }
}
