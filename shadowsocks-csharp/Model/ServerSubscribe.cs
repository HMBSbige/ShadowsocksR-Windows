using System;

namespace Shadowsocks.Model
{
    [Serializable]
    public class ServerSubscribe
    {
        private static string DEFAULT_FEED_URL = @"https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/freenodeplain.txt";

        public string URL = DEFAULT_FEED_URL;
        public string Group;
        public ulong LastUpdateTime;
    }
}