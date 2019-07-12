using System;

namespace Shadowsocks.Model
{
    [Serializable]
    public class ServerSpeedLogShow
    {
        public long totalConnectTimes;
        public long totalDisconnectTimes;
        public long errorConnectTimes;
        public long errorTimeoutTimes;
        public long errorDecodeTimes;
        public long errorEmptyTimes;
        public long errorContinurousTimes;
        public long errorLogTimes;
        public long totalUploadBytes;
        public long totalDownloadBytes;
        public long totalDownloadRawBytes;
        public long avgConnectTime;
        public long avgDownloadBytes;
        public long maxDownloadBytes;
        public long avgUploadBytes;
        public long maxUploadBytes;
    }
}