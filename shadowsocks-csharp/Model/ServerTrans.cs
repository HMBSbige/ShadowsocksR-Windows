using System;

namespace Shadowsocks.Model
{
    [Serializable]
    public class ServerTrans
    {
        public long totalUploadBytes;
        public long totalDownloadBytes;
    }
}