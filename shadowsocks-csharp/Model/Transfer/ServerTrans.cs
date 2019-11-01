using System;

namespace Shadowsocks.Model.Transfer
{
    [Serializable]
    public class ServerTrans
    {
        public long TotalUploadBytes;
        public long TotalDownloadBytes;
    }
}