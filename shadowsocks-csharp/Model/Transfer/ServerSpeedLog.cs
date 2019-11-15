using Shadowsocks.Util;
using Shadowsocks.ViewModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
// ReSharper disable MemberCanBePrivate.Global

namespace Shadowsocks.Model.Transfer
{
    public class ServerSpeedLog : ViewModelBase
    {
        private long _totalConnectTimes;
        private long _totalDisconnectTimes;
        private long _errorConnectTimes;
        private long _errorTimeoutTimes;
        private long _errorDecodeTimes;
        private long _errorEmptyTimes;
        private long _errorContinuousTimes;
        private long _transUpload;
        private long _transDownload;
        private long _transDownloadRaw;
        private readonly List<TransLog> _downTransLog = new List<TransLog>();
        private readonly List<TransLog> _upTransLog = new List<TransLog>();
        private long _maxTransDownload;
        private long _maxTransUpload;
        private long _avgConnectTime = -1;
        private readonly ConcurrentQueue<ErrorLog> _errList = new ConcurrentQueue<ErrorLog>();

        private const int AvgTime = 5;

        public ServerSpeedLog() { }

        public ServerSpeedLog(long upload, long download)
        {
            Interlocked.Exchange(ref _transUpload, upload);
            Interlocked.Exchange(ref _transDownload, download);
        }

        public void GetTransSpeed(out long upload, out long download)
        {
            upload = MaxUpSpeed;
            download = MaxDownSpeed;
        }

        /// <summary>
        /// 总连接数
        /// </summary>
        public long TotalConnectTimes => Interlocked.Read(ref _totalConnectTimes);

        public long TotalDisconnectTimes => Interlocked.Read(ref _totalDisconnectTimes);

        /// <summary>
        /// 连接数
        /// </summary>
        public long Connecting => TotalConnectTimes - TotalDisconnectTimes;

        #region 下载速度

        public long AvgDownloadBytes
        {
            get
            {
                List<TransLog> transLog;
                lock (this)
                {
                    transLog = _downTransLog.ToList();
                }
                double totalTime = 0;
                if (transLog.Count == 0 || transLog.Count > 0 &&
                    DateTime.Now > transLog.Last().recvTime.AddSeconds(AvgTime))
                {
                    return 0;
                }

                var totalBytes = transLog.Aggregate<TransLog, long>(0, (current, t) => current + t.size);
                totalBytes -= transLog[0].firstsize;

                if (transLog.Count > 1)
                    totalTime = (transLog.Last().endTime - transLog[0].recvTime).TotalSeconds;
                if (totalTime > 0.2)
                {
                    var ret = (long)(totalBytes / totalTime);
                    return ret;
                }

                return 0;
            }
        }

        public string AvgDownloadBytesText => $@"{Utils.FormatBytes(AvgDownloadBytes)}/s";

        #endregion

        #region 上传速度

        public long AvgUploadBytes
        {
            get
            {
                List<TransLog> transLog;
                lock (this)
                {
                    transLog = _upTransLog.ToList();
                }
                double totalTime = 0;
                if (transLog.Count == 0 || transLog.Count > 0 &&
                    DateTime.Now > transLog.Last().recvTime.AddSeconds(AvgTime))
                {
                    return 0;
                }

                var totalBytes = transLog.Aggregate<TransLog, long>(0, (current, t) => current + t.size);
                totalBytes -= transLog[0].firstsize;

                if (transLog.Count > 1)
                    totalTime = (transLog.Last().endTime - transLog[0].recvTime).TotalSeconds;
                if (totalTime > 0.2)
                {
                    var ret = (long)(totalBytes / totalTime);
                    return ret;
                }

                return 0;
            }
        }

        public string AvgUploadBytesText => $@"{Utils.FormatBytes(AvgUploadBytes)}/s";

        #endregion

        #region 最大下载速度

        public long MaxDownSpeed => Interlocked.Read(ref _maxTransDownload);

        public string MaxDownSpeedText => $@"{Utils.FormatBytes(MaxDownSpeed)}/s";

        #endregion

        #region 最大上传速度

        public long MaxUpSpeed => Interlocked.Read(ref _maxTransUpload);

        public string MaxUpSpeedText => $@"{Utils.FormatBytes(MaxUpSpeed)}/s";

        #endregion

        #region 延迟

        public long AvgConnectTime => Interlocked.Read(ref _avgConnectTime);

        public long AvgConnectTimeText
        {
            get
            {
                if (AvgConnectTime > 0)
                {
                    return AvgConnectTime / 1000;
                }

                return 0;
            }
        }

        #endregion

        #region 总上传

        public long TotalUploadBytes => Interlocked.Read(ref _transUpload);

        public string TotalUploadBytesText => Utils.FormatBytes(TotalUploadBytes);

        #endregion

        #region 总下载

        public long TotalDownloadBytes => Interlocked.Read(ref _transDownload);

        public string TotalDownloadBytesText => Utils.FormatBytes(TotalDownloadBytes);

        #endregion

        #region 实下载

        public long TotalDownloadRawBytes => Interlocked.Read(ref _transDownloadRaw);

        public string TotalDownloadRawBytesText => Utils.FormatBytes(TotalDownloadRawBytes);

        #endregion

        /// <summary>
        /// 错误
        /// </summary>
        public long ConnectError => ErrorConnectTimes + ErrorDecodeTimes;

        public long ErrorConnectTimes => Interlocked.Read(ref _errorConnectTimes);

        public long ErrorDecodeTimes => Interlocked.Read(ref _errorDecodeTimes);

        /// <summary>
        /// 连错
        /// </summary>
        public long ErrorContinuousTimes => Interlocked.Read(ref _errorContinuousTimes);

        /// <summary>
        /// 超时
        /// </summary>
        public long ErrorTimeoutTimes => Interlocked.Read(ref _errorTimeoutTimes);

        /// <summary>
        /// 空连
        /// </summary>
        public long ErrorEmptyTimes => Interlocked.Read(ref _errorEmptyTimes);

        #region 出错比例

        public double? ErrorPercent
        {
            get
            {
                var errorLogTimes = _errList.Count;
                var m = errorLogTimes + Connecting;
                if (m > 0)
                {
                    return (ConnectError + ErrorTimeoutTimes) * 100.0 / m;
                }

                return null;
            }
        }

        #endregion

        public void ClearTrans()
        {
            Interlocked.Exchange(ref _transUpload, 0);
            Interlocked.Exchange(ref _transDownload, 0);

            OnPropertyChanged(nameof(TotalUploadBytes));
            OnPropertyChanged(nameof(TotalUploadBytesText));
            OnPropertyChanged(nameof(TotalDownloadBytes));
            OnPropertyChanged(nameof(TotalDownloadBytesText));
        }

        public void ClearError()
        {
            var value = Connecting;
            if (value > 0)
            {
                Interlocked.Exchange(ref _totalConnectTimes, value);
            }
            else
            {
                Interlocked.Exchange(ref _totalConnectTimes, 0);
            }
            Interlocked.Exchange(ref _totalDisconnectTimes, 0);
            Interlocked.Exchange(ref _errorConnectTimes, 0);
            Interlocked.Exchange(ref _errorTimeoutTimes, 0);
            Interlocked.Exchange(ref _errorDecodeTimes, 0);
            Interlocked.Exchange(ref _errorEmptyTimes, 0);
            Interlocked.Exchange(ref _errorContinuousTimes, 0);
            _errList.Clear();

            OnPropertyChanged(nameof(Connecting));
            OnPropertyChanged(nameof(TotalConnectTimes));
            OnPropertyChanged(nameof(TotalDisconnectTimes));
            OnPropertyChanged(nameof(ConnectError));
            OnPropertyChanged(nameof(ErrorConnectTimes));
            OnPropertyChanged(nameof(ErrorTimeoutTimes));
            OnPropertyChanged(nameof(ErrorDecodeTimes));
            OnPropertyChanged(nameof(ErrorEmptyTimes));
            OnPropertyChanged(nameof(ErrorContinuousTimes));
            OnPropertyChanged(nameof(ErrorPercent));
        }

        public void ClearMaxSpeed()
        {
            Interlocked.Exchange(ref _maxTransDownload, 0);
            Interlocked.Exchange(ref _maxTransUpload, 0);

            OnPropertyChanged(nameof(MaxDownSpeed));
            OnPropertyChanged(nameof(MaxDownSpeedText));
            OnPropertyChanged(nameof(MaxUpSpeed));
            OnPropertyChanged(nameof(MaxUpSpeedText));
        }

        public void Clear()
        {
            ClearError();
            ClearMaxSpeed();
            ClearTrans();
            Interlocked.Exchange(ref _transDownloadRaw, 0);
            OnPropertyChanged(nameof(TotalDownloadRawBytes));
            OnPropertyChanged(nameof(TotalDownloadRawBytesText));
        }

        public void AddConnectTimes()
        {
            Interlocked.Increment(ref _totalConnectTimes);
            OnPropertyChanged(nameof(TotalConnectTimes));
            OnPropertyChanged(nameof(Connecting));
            OnPropertyChanged(nameof(ErrorPercent));
        }

        public void AddDisconnectTimes()
        {
            Interlocked.Increment(ref _totalDisconnectTimes);
            OnPropertyChanged(nameof(TotalDisconnectTimes));
            OnPropertyChanged(nameof(Connecting));
            OnPropertyChanged(nameof(ErrorPercent));
        }

        private void Sweep()
        {
            while (_errList.TryPeek(out var first))
            {
                if ((DateTime.Now - first.time).TotalMinutes < 30 && _errList.Count < 100)
                {
                    break;
                }
                _errList.TryDequeue(out first);
                switch (first.errno)
                {
                    case 1:
                    {
                        if (ErrorConnectTimes > 0)
                        {
                            Interlocked.Decrement(ref _errorConnectTimes);
                            OnPropertyChanged(nameof(ErrorConnectTimes));
                            OnPropertyChanged(nameof(ConnectError));
                        }

                        break;
                    }
                    case 2:
                    {
                        if (ErrorTimeoutTimes > 0)
                        {
                            Interlocked.Decrement(ref _errorTimeoutTimes);
                            OnPropertyChanged(nameof(ErrorTimeoutTimes));
                        }

                        break;
                    }
                    case 3:
                    {
                        if (ErrorDecodeTimes > 0)
                        {
                            Interlocked.Decrement(ref _errorDecodeTimes);
                            OnPropertyChanged(nameof(ErrorDecodeTimes));
                            OnPropertyChanged(nameof(ConnectError));
                        }

                        break;
                    }
                    case 4:
                    {
                        if (ErrorEmptyTimes > 0)
                        {
                            Interlocked.Decrement(ref _errorEmptyTimes);
                            OnPropertyChanged(nameof(ErrorEmptyTimes));
                        }

                        break;
                    }
                }
                OnPropertyChanged(nameof(ErrorPercent));
            }
        }

        public void AddNoErrorTimes()
        {
            _errList.Enqueue(new ErrorLog(0));
            Sweep();

            Interlocked.Exchange(ref _errorEmptyTimes, 0);
            OnPropertyChanged(nameof(ErrorEmptyTimes));
        }

        public void AddErrorTimes()
        {
            Interlocked.Increment(ref _errorConnectTimes);
            Interlocked.Add(ref _errorContinuousTimes, 2);
            _errList.Enqueue(new ErrorLog(1));
            Sweep();

            OnPropertyChanged(nameof(ErrorConnectTimes));
            OnPropertyChanged(nameof(ErrorContinuousTimes));
            OnPropertyChanged(nameof(ConnectError));
        }

        public void AddTimeoutTimes()
        {
            Interlocked.Increment(ref _errorTimeoutTimes);
            Interlocked.Increment(ref _errorContinuousTimes);
            _errList.Enqueue(new ErrorLog(2));
            Sweep();

            OnPropertyChanged(nameof(ErrorTimeoutTimes));
            OnPropertyChanged(nameof(ErrorContinuousTimes));
            OnPropertyChanged(nameof(ConnectError));
        }

        public void AddErrorDecodeTimes()
        {
            Interlocked.Increment(ref _errorDecodeTimes);
            Interlocked.Add(ref _errorContinuousTimes, 10);
            _errList.Enqueue(new ErrorLog(3));
            Sweep();

            OnPropertyChanged(nameof(ErrorDecodeTimes));
            OnPropertyChanged(nameof(ErrorContinuousTimes));
            OnPropertyChanged(nameof(ConnectError));
        }

        public void AddErrorEmptyTimes()
        {
            Interlocked.Increment(ref _errorEmptyTimes);
            Interlocked.Increment(ref _errorContinuousTimes);
            _errList.Enqueue(new ErrorLog(0));
            Sweep();

            OnPropertyChanged(nameof(ErrorEmptyTimes));
            OnPropertyChanged(nameof(ErrorContinuousTimes));
            OnPropertyChanged(nameof(ConnectError));
        }

        private static void UpdateTransLog(IList<TransLog> transLog, int bytes, DateTime now, ref long maxTrans, bool updateMaxTrans)
        {
            if (transLog.Count > 0)
            {
                const int baseTimeDiff = 100;
                const int maxTimeDiff = 3 * baseTimeDiff;
                var timeDiff = (int)(now - transLog.Last().recvTime).TotalMilliseconds;
                if (timeDiff < 0)
                {
                    transLog.Clear();
                    transLog.Add(new TransLog(bytes, now));
                    return;
                }
                if (timeDiff < baseTimeDiff)
                {
                    transLog.Last().times++;
                    transLog.Last().size += bytes;
                    if (transLog.Last().endTime < now)
                        transLog.Last().endTime = now;
                }
                else
                {
                    transLog.Add(new TransLog(bytes, now));

                    var baseTimes = 1 + (maxTrans > 1024 * 512 ? 1 : 0);
                    var lastIndex = transLog.Count - 1 - 2;
                    if (updateMaxTrans && transLog.Count >= 6 && transLog[lastIndex].times > baseTimes)
                    {
                        var beginIndex = lastIndex - 1;
                        for (; beginIndex > 0; --beginIndex)
                        {
                            if ((transLog[beginIndex + 1].recvTime - transLog[beginIndex].endTime).TotalMilliseconds > maxTimeDiff
                            || transLog[beginIndex].times <= baseTimes
                            )
                            {
                                break;
                            }
                        }

                        if (beginIndex <= lastIndex - 4)
                        {
                            beginIndex++;
                            var t = new TransLog(transLog[beginIndex].firstsize, transLog[beginIndex].recvTime)
                            {
                                endTime = transLog[lastIndex].endTime,
                                size = 0
                            };
                            for (var i = beginIndex; i <= lastIndex; ++i)
                            {
                                t.size += transLog[i].size;
                            }

                            if (maxTrans == 0)
                            {
                                maxTrans = (long)((t.size - t.firstsize) / (t.endTime - t.recvTime).TotalSeconds * 0.7);
                            }
                            else
                            {
                                const double a = 2.0 / (1 + 32);
                                maxTrans = (long)(0.5 + maxTrans * (1 - a) + a * ((t.size - t.firstsize) / (t.endTime - t.recvTime).TotalSeconds));
                            }
                        }
                    }

                }
                while (transLog.Count > 0 && now > transLog[0].recvTime.AddSeconds(AvgTime))
                {
                    transLog.RemoveAt(0);
                }
            }
            else
            {
                transLog.Add(new TransLog(bytes, now));
            }
        }

        public void AddUploadBytes(int bytes, DateTime now, bool updateMaxTrans)
        {
            Interlocked.Add(ref _transUpload, bytes);
            OnPropertyChanged(nameof(TotalUploadBytes));
            OnPropertyChanged(nameof(TotalUploadBytesText));
            lock (this)
            {
                UpdateTransLog(_upTransLog, bytes, now, ref _maxTransUpload, updateMaxTrans);
            }

            OnPropertyChanged(nameof(AvgUploadBytes));
            OnPropertyChanged(nameof(AvgUploadBytesText));
            OnPropertyChanged(nameof(MaxUpSpeed));
            OnPropertyChanged(nameof(MaxUpSpeedText));
        }

        public void AddDownloadBytes(int bytes, DateTime now, bool updateMaxTrans)
        {
            Interlocked.Add(ref _transDownload, bytes);
            OnPropertyChanged(nameof(TotalDownloadBytes));
            OnPropertyChanged(nameof(TotalDownloadBytesText));
            lock (this)
            {
                UpdateTransLog(_downTransLog, bytes, now, ref _maxTransDownload, updateMaxTrans);
            }

            OnPropertyChanged(nameof(AvgDownloadBytes));
            OnPropertyChanged(nameof(AvgDownloadBytesText));
            OnPropertyChanged(nameof(MaxDownSpeed));
            OnPropertyChanged(nameof(MaxDownSpeedText));
        }

        public void AddDownloadRawBytes(long bytes)
        {
            Interlocked.Add(ref _transDownloadRaw, bytes);
            OnPropertyChanged(nameof(TotalDownloadRawBytes));
            OnPropertyChanged(nameof(TotalDownloadRawBytesText));
        }

        public void ResetErrorDecodeTimes()
        {
            Interlocked.Exchange(ref _errorDecodeTimes, 0);
            Interlocked.Exchange(ref _errorEmptyTimes, 0);
            Interlocked.Exchange(ref _errorContinuousTimes, 0);

            OnPropertyChanged(nameof(ErrorDecodeTimes));
            OnPropertyChanged(nameof(ErrorEmptyTimes));
            OnPropertyChanged(nameof(ErrorContinuousTimes));
            OnPropertyChanged(nameof(ConnectError));
            OnPropertyChanged(nameof(ErrorPercent));
        }

        public void ResetContinuousTimes()
        {
            Interlocked.Exchange(ref _errorEmptyTimes, 0);
            Interlocked.Exchange(ref _errorContinuousTimes, 0);

            OnPropertyChanged(nameof(ErrorEmptyTimes));
            OnPropertyChanged(nameof(ErrorContinuousTimes));
        }

        public void ResetEmptyTimes()
        {
            Interlocked.Exchange(ref _errorEmptyTimes, 0);
            OnPropertyChanged(nameof(ErrorEmptyTimes));
        }

        public void AddConnectTime(long millisecond)
        {
            if (millisecond == 0)
            {
                millisecond = 10;
            }
            else
            {
                millisecond *= 1000;
            }

            var oldValue = AvgConnectTime;
            if (oldValue == -1)
            {
                Interlocked.Exchange(ref _avgConnectTime, millisecond);
            }
            else
            {
                const double a = 2.0 / (1 + 16);
                Interlocked.Exchange(ref _avgConnectTime, Convert.ToInt64(0.5 + oldValue * (1 - a) + a * millisecond));
            }
            OnPropertyChanged(nameof(AvgConnectTimeText));
        }
    }

}
