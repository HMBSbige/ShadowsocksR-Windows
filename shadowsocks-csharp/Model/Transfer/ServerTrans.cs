using System;

namespace Shadowsocks.Model.Transfer
{
    [Serializable]
    public class ServerTrans
    {
        public long TotalUploadBytes { get; set; }
        public long TotalDownloadBytes { get; set; }
    }
}
