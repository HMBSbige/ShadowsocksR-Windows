using Shadowsocks.Util;
using Shadowsocks.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Shadowsocks.Model.Transfer
{
    public class ServerSpeedLog : ViewModelBase
    {
        private long totalConnectTimes;
        private long totalDisconnectTimes;
        private long errorConnectTimes;
        private long errorTimeoutTimes;
        private long errorDecodeTimes;
        private long errorEmptyTimes;
        private long errorContinuousTimes;
        private long transUpload;
        private long transDownload;
        private long transDownloadRaw;
        private readonly List<TransLog> downTransLog = new List<TransLog>();
        private readonly List<TransLog> upTransLog = new List<TransLog>();
        private long maxTransDownload;
        private long maxTransUpload;
        private long avgConnectTime = -1;
        private readonly LinkedList<ErrorLog> errList = new LinkedList<ErrorLog>();

        private const int AvgTime = 5;

        public ServerSpeedLog() { }

        public ServerSpeedLog(long upload, long download)
        {
            Interlocked.Exchange(ref transUpload, upload);
            Interlocked.Exchange(ref transDownload, download);
        }

        public void GetTransSpeed(out long upload, out long download)
        {
            upload = MaxUpSpeed;
            download = MaxDownSpeed;
        }

        /// <summary>
        /// 总连接数
        /// </summary>
        public long TotalConnectTimes => Interlocked.Read(ref totalConnectTimes);

        public long TotalDisconnectTimes => Interlocked.Read(ref totalDisconnectTimes);

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
                    transLog = downTransLog.ToList();
                }
                {
                    double totalTime = 0;
                    if (transLog.Count == 0 || transLog.Count > 0 && DateTime.Now > transLog[transLog.Count - 1].recvTime.AddSeconds(AvgTime))
                    {
                        return 0;
                    }

                    var totalBytes = transLog.Aggregate<TransLog, long>(0, (current, t) => current + t.size);
                    totalBytes -= transLog[0].firstsize;

                    if (transLog.Count > 1)
                        totalTime = (transLog[transLog.Count - 1].endTime - transLog[0].recvTime).TotalSeconds;
                    if (totalTime > 0.2)
                    {
                        var ret = (long)(totalBytes / totalTime);
                        return ret;
                    }

                    return 0;
                }
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
                    if (upTransLog == null)
                        return 0;
                    transLog = upTransLog.ToList();
                }
                {
                    double totalTime = 0;
                    if (transLog.Count == 0 || transLog.Count > 0 && DateTime.Now > transLog[transLog.Count - 1].recvTime.AddSeconds(AvgTime))
                    {
                        return 0;
                    }

                    var totalBytes = transLog.Aggregate<TransLog, long>(0, (current, t) => current + t.size);
                    totalBytes -= transLog[0].firstsize;

                    if (transLog.Count > 1)
                        totalTime = (transLog[transLog.Count - 1].endTime - transLog[0].recvTime).TotalSeconds;
                    if (totalTime > 0.2)
                    {
                        var ret = (long)(totalBytes / totalTime);
                        return ret;
                    }

                    return 0;
                }
            }
        }

        public string AvgUploadBytesText => $@"{Utils.FormatBytes(AvgUploadBytes)}/s";

        #endregion

        #region 最大下载速度

        public long MaxDownSpeed => Interlocked.Read(ref maxTransDownload);

        public string MaxDownSpeedText => $@"{Utils.FormatBytes(MaxDownSpeed)}/s";

        #endregion

        #region 最大上传速度

        public long MaxUpSpeed => Interlocked.Read(ref maxTransUpload);

        public string MaxUpSpeedText => $@"{Utils.FormatBytes(MaxUpSpeed)}/s";

        #endregion

        #region 延迟

        public long AvgConnectTime => Interlocked.Read(ref avgConnectTime);

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

        public long TotalUploadBytes => Interlocked.Read(ref transUpload);

        public string TotalUploadBytesText => Utils.FormatBytes(TotalUploadBytes);

        #endregion

        #region 总下载

        public long TotalDownloadBytes => Interlocked.Read(ref transDownload);

        public string TotalDownloadBytesText => Utils.FormatBytes(TotalDownloadBytes);

        #endregion

        #region 实下载

        public long TotalDownloadRawBytes => Interlocked.Read(ref transDownloadRaw);

        public string TotalDownloadRawBytesText => Utils.FormatBytes(TotalDownloadRawBytes);

        #endregion

        /// <summary>
        /// 错误
        /// </summary>
        public long ConnectError => ErrorConnectTimes + ErrorDecodeTimes;

        public long ErrorConnectTimes => Interlocked.Read(ref errorConnectTimes);

        public long ErrorDecodeTimes => Interlocked.Read(ref errorDecodeTimes);

        /// <summary>
        /// 连错
        /// </summary>
        public long ErrorContinuousTimes => Interlocked.Read(ref errorContinuousTimes);

        /// <summary>
        /// 超时
        /// </summary>
        public long ErrorTimeoutTimes => Interlocked.Read(ref errorTimeoutTimes);

        /// <summary>
        /// 空连
        /// </summary>
        public long ErrorEmptyTimes => Interlocked.Read(ref errorEmptyTimes);

        #region 出错比例

        public double? ErrorPercent
        {
            get
            {
                int errorLogTimes;
                lock (this)
                {
                    errorLogTimes = errList.Count;
                }

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
            Interlocked.Exchange(ref transUpload, 0);
            Interlocked.Exchange(ref transDownload, 0);

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
                Interlocked.Exchange(ref totalConnectTimes, value);
            }
            else
            {
                Interlocked.Exchange(ref totalConnectTimes, 0);
            }
            Interlocked.Exchange(ref totalDisconnectTimes, 0);
            Interlocked.Exchange(ref errorConnectTimes, 0);
            Interlocked.Exchange(ref errorTimeoutTimes, 0);
            Interlocked.Exchange(ref errorDecodeTimes, 0);
            Interlocked.Exchange(ref errorEmptyTimes, 0);
            Interlocked.Exchange(ref errorContinuousTimes, 0);
            lock (this)
            {
                errList.Clear();
            }

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
            Interlocked.Exchange(ref maxTransDownload, 0);
            Interlocked.Exchange(ref maxTransUpload, 0);

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
            Interlocked.Exchange(ref transDownloadRaw, 0);
            OnPropertyChanged(nameof(TotalDownloadRawBytes));
            OnPropertyChanged(nameof(TotalDownloadRawBytesText));
        }

        public void AddConnectTimes()
        {
            Interlocked.Increment(ref totalConnectTimes);
            OnPropertyChanged(nameof(TotalConnectTimes));
            OnPropertyChanged(nameof(Connecting));
            OnPropertyChanged(nameof(ErrorPercent));
        }

        public void AddDisconnectTimes()
        {
            Interlocked.Increment(ref totalDisconnectTimes);
            OnPropertyChanged(nameof(TotalDisconnectTimes));
            OnPropertyChanged(nameof(Connecting));
            OnPropertyChanged(nameof(ErrorPercent));
        }

        protected void Sweep()
        {
            while (errList.Count > 0)
            {
                if ((DateTime.Now - errList.First.Value.time).TotalMinutes < 30 && errList.Count < 100)
                {
                    break;
                }

                var errCode = errList.First.Value.errno;
                errList.RemoveFirst();
                if (errCode == 1)
                {
                    if (ErrorConnectTimes > 0)
                    {
                        Interlocked.Decrement(ref errorConnectTimes);
                        OnPropertyChanged(nameof(ErrorConnectTimes));
                        OnPropertyChanged(nameof(ConnectError));
                    }
                }
                else if (errCode == 2)
                {
                    if (ErrorTimeoutTimes > 0)
                    {
                        Interlocked.Decrement(ref errorTimeoutTimes);
                        OnPropertyChanged(nameof(ErrorTimeoutTimes));
                    }
                }
                else if (errCode == 3)
                {
                    if (ErrorDecodeTimes > 0)
                    {
                        Interlocked.Decrement(ref errorDecodeTimes);
                        OnPropertyChanged(nameof(ErrorDecodeTimes));
                        OnPropertyChanged(nameof(ConnectError));
                    }
                }
                else if (errCode == 4)
                {
                    if (ErrorEmptyTimes > 0)
                    {
                        Interlocked.Decrement(ref errorEmptyTimes);
                        OnPropertyChanged(nameof(ErrorEmptyTimes));
                    }
                }
                OnPropertyChanged(nameof(ErrorPercent));
            }
        }

        public void AddNoErrorTimes()
        {
            lock (this)
            {
                errList.AddLast(new ErrorLog(0));
                Sweep();
            }
            Interlocked.Exchange(ref errorEmptyTimes, 0);
            OnPropertyChanged(nameof(ErrorEmptyTimes));
        }

        public void AddErrorTimes()
        {
            Interlocked.Increment(ref errorConnectTimes);
            Interlocked.Add(ref errorContinuousTimes, 2);

            lock (this)
            {
                errList.AddLast(new ErrorLog(1));
                Sweep();
            }

            OnPropertyChanged(nameof(ErrorConnectTimes));
            OnPropertyChanged(nameof(ErrorContinuousTimes));
            OnPropertyChanged(nameof(ConnectError));
        }

        public void AddTimeoutTimes()
        {
            Interlocked.Increment(ref errorTimeoutTimes);
            Interlocked.Increment(ref errorContinuousTimes);
            lock (this)
            {
                errList.AddLast(new ErrorLog(2));
                Sweep();
            }

            OnPropertyChanged(nameof(ErrorTimeoutTimes));
            OnPropertyChanged(nameof(ErrorContinuousTimes));
            OnPropertyChanged(nameof(ConnectError));
        }

        public void AddErrorDecodeTimes()
        {
            Interlocked.Increment(ref errorDecodeTimes);
            Interlocked.Add(ref errorContinuousTimes, 10);
            lock (this)
            {
                errList.AddLast(new ErrorLog(3));
                Sweep();
            }

            OnPropertyChanged(nameof(ErrorDecodeTimes));
            OnPropertyChanged(nameof(ErrorContinuousTimes));
            OnPropertyChanged(nameof(ConnectError));
        }

        public void AddErrorEmptyTimes()
        {
            Interlocked.Increment(ref errorEmptyTimes);
            Interlocked.Increment(ref errorContinuousTimes);
            lock (this)
            {
                errList.AddLast(new ErrorLog(0));
                Sweep();
            }

            OnPropertyChanged(nameof(ErrorEmptyTimes));
            OnPropertyChanged(nameof(ErrorContinuousTimes));
            OnPropertyChanged(nameof(ConnectError));
        }

        private static void UpdateTransLog(IList<TransLog> transLog, int bytes, DateTime now, ref long maxTrans, bool updateMaxTrans)
        {
            if (transLog.Count > 0)
            {
                const int base_time_diff = 100;
                const int max_time_diff = 3 * base_time_diff;
                var time_diff = (int)(now - transLog[transLog.Count - 1].recvTime).TotalMilliseconds;
                if (time_diff < 0)
                {
                    transLog.Clear();
                    transLog.Add(new TransLog(bytes, now));
                    return;
                }
                if (time_diff < base_time_diff)
                {
                    transLog[transLog.Count - 1].times++;
                    transLog[transLog.Count - 1].size += bytes;
                    if (transLog[transLog.Count - 1].endTime < now)
                        transLog[transLog.Count - 1].endTime = now;
                }
                else
                {
                    if (time_diff >= 0)
                    {
                        transLog.Add(new TransLog(bytes, now));

                        var base_times = 1 + (maxTrans > 1024 * 512 ? 1 : 0);
                        var last_index = transLog.Count - 1 - 2;
                        if (updateMaxTrans && transLog.Count >= 6 && transLog[last_index].times > base_times)
                        {
                            var begin_index = last_index - 1;
                            for (; begin_index > 0; --begin_index)
                            {
                                if ((transLog[begin_index + 1].recvTime - transLog[begin_index].endTime).TotalMilliseconds > max_time_diff
                                    || transLog[begin_index].times <= base_times
                                    )
                                {
                                    break;
                                }
                            }
                            if (begin_index <= last_index - 4)
                            {
                                begin_index++;
                                var t = new TransLog(transLog[begin_index].firstsize, transLog[begin_index].recvTime)
                                {
                                    endTime = transLog[last_index].endTime,
                                    size = 0
                                };
                                for (var i = begin_index; i <= last_index; ++i)
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
                    else
                    {
                        var i = transLog.Count - 1;
                        for (; i >= 0; --i)
                        {
                            if (transLog[i].recvTime > now && i > 0)
                                continue;

                            transLog[i].times += 1;
                            transLog[i].size += bytes;
                            if (transLog[i].endTime < now)
                                transLog[i].endTime = now;

                            break;
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
            Interlocked.Add(ref transUpload, bytes);
            OnPropertyChanged(nameof(TotalUploadBytes));
            OnPropertyChanged(nameof(TotalUploadBytesText));
            lock (this)
            {
                UpdateTransLog(upTransLog, bytes, now, ref maxTransUpload, updateMaxTrans);
            }

            OnPropertyChanged(nameof(AvgUploadBytes));
            OnPropertyChanged(nameof(AvgUploadBytesText));
            OnPropertyChanged(nameof(MaxUpSpeed));
            OnPropertyChanged(nameof(MaxUpSpeedText));
        }

        public void AddDownloadBytes(int bytes, DateTime now, bool updateMaxTrans)
        {
            Interlocked.Add(ref transDownload, bytes);
            OnPropertyChanged(nameof(TotalDownloadBytes));
            OnPropertyChanged(nameof(TotalDownloadBytesText));
            lock (this)
            {
                UpdateTransLog(downTransLog, bytes, now, ref maxTransDownload, updateMaxTrans);
            }

            OnPropertyChanged(nameof(AvgDownloadBytes));
            OnPropertyChanged(nameof(AvgDownloadBytesText));
            OnPropertyChanged(nameof(MaxDownSpeed));
            OnPropertyChanged(nameof(MaxDownSpeedText));
        }

        public void AddDownloadRawBytes(long bytes)
        {
            Interlocked.Add(ref transDownloadRaw, bytes);
            OnPropertyChanged(nameof(TotalDownloadRawBytes));
            OnPropertyChanged(nameof(TotalDownloadRawBytesText));
        }

        public void ResetErrorDecodeTimes()
        {
            Interlocked.Exchange(ref errorDecodeTimes, 0);
            Interlocked.Exchange(ref errorEmptyTimes, 0);
            Interlocked.Exchange(ref errorContinuousTimes, 0);

            OnPropertyChanged(nameof(ErrorDecodeTimes));
            OnPropertyChanged(nameof(ErrorEmptyTimes));
            OnPropertyChanged(nameof(ErrorContinuousTimes));
            OnPropertyChanged(nameof(ConnectError));
            OnPropertyChanged(nameof(ErrorPercent));
        }

        public void ResetContinuousTimes()
        {
            Interlocked.Exchange(ref errorEmptyTimes, 0);
            Interlocked.Exchange(ref errorContinuousTimes, 0);

            OnPropertyChanged(nameof(ErrorEmptyTimes));
            OnPropertyChanged(nameof(ErrorContinuousTimes));
        }

        public void ResetEmptyTimes()
        {
            Interlocked.Exchange(ref errorEmptyTimes, 0);
            OnPropertyChanged(nameof(ErrorEmptyTimes));
        }

        public void AddConnectTime(long millisecond)
        {
            lock (this)
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
                    Interlocked.Exchange(ref avgConnectTime, millisecond);
                }
                else
                {
                    const double a = 2.0 / (1 + 16);
                    Interlocked.Exchange(ref avgConnectTime, (int)(0.5 + oldValue * (1 - a) + a * millisecond));
                }
            }
            OnPropertyChanged(nameof(AvgConnectTimeText));
        }
    }

}
