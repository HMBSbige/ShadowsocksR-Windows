using System;
using Shadowsocks.Model.Transfer;
#if DEBUG
using System.Collections.Generic;
#endif

namespace Shadowsocks.Controller.Service
{
    internal class SpeedTester
    {
#if DEBUG
        private struct TransLog
        {
            public int Dir;
            public int Size;
        }
#endif
        public DateTime TimeConnectBegin;
        public DateTime TimeConnectEnd;
        public DateTime TimeBeginUpload;
        public DateTime TimeBeginDownload;
        public long SizeUpload;
        public long SizeDownload;
        public long SizeProtocolRecv;
        public long SizeRecv;
#if DEBUG
        private readonly List<TransLog> _sizeTransfer = new List<TransLog>();
#endif
        public string ServerId;
        public ServerTransferTotal Transfer;
        private int _uploadCnt;
        private int _downloadCnt;

        public void BeginConnect()
        {
            TimeConnectBegin = DateTime.Now;
        }

        public void EndConnect()
        {
            TimeConnectEnd = DateTime.Now;
        }

        public void BeginUpload()
        {
            TimeBeginUpload = DateTime.Now;
        }

        public bool BeginDownload()
        {
            if (TimeBeginDownload == new DateTime())
            {
                TimeBeginDownload = DateTime.Now;
                return true;
            }
            return false;
        }

        public bool AddDownloadSize(int size)
        {
            SizeDownload += size;
            if (Transfer != null && ServerId != null)
            {
                Transfer.AddDownload(ServerId, size);
            }
            _uploadCnt = 0;
            _downloadCnt += 1;
#if DEBUG
            if (_sizeTransfer.Count < 1024 * 128)
            {
                lock (_sizeTransfer)
                {
                    _sizeTransfer.Add(new TransLog { Dir = 1, Size = size });
                }
            }
#endif
            return _downloadCnt > 30;
        }

        public void AddProtocolRecvSize(int size)
        {
            SizeProtocolRecv += size;
        }

        public void AddRecvSize(int size)
        {
            SizeRecv += size;
        }

        public bool AddUploadSize(int size)
        {
            SizeUpload += size;
            if (Transfer != null && ServerId != null)
            {
                Transfer.AddUpload(ServerId, size);
            }
            _uploadCnt += 1;
            _downloadCnt = 0;
#if DEBUG
            if (_sizeTransfer.Count < 1024 * 128)
            {
                lock (_sizeTransfer)
                {
                    _sizeTransfer.Add(new TransLog { Dir = 0, Size = size });
                }
            }
#endif
            return _uploadCnt > 30;
        }

        public string TransferLog()
        {
            var ret = string.Empty;
#if DEBUG
            var lastDir = -1;
            foreach (var t in _sizeTransfer)
            {
                if (t.Dir != lastDir)
                {
                    lastDir = t.Dir;
                    ret += t.Dir == 0 ? " u" : " d";
                }
                ret += $" {t.Size}";
            }
#endif
            return ret;
        }
    }
}
