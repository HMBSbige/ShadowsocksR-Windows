using System;
using System.Collections.Generic;
using System.Text;

namespace Shadowsocks.Model
{
    public class TransLog
    {
        public int size;
        public int firstsize;
        public int times;
        public DateTime recvTime;
        public DateTime endTime;
        public TransLog(int s, DateTime t)
        {
            firstsize = s;
            size = s;
            recvTime = t;
            endTime = t;
            times = 1;
        }
    }

    public class ErrorLog
    {
        public int errno;
        public DateTime time;
        public ErrorLog(int no)
        {
            errno = no;
            time = DateTime.Now;
        }
    }

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
    public class ServerSpeedLog
    {
        private long totalConnectTimes = 0;
        private long totalDisconnectTimes = 0;
        private long errorConnectTimes = 0;
        private long errorTimeoutTimes = 0;
        private long errorDecodeTimes = 0;
        private long errorEmptyTimes = 0;
        private long errorContinurousTimes = 0;
        private long transUpload = 0;
        private long transDownload = 0;
        private long transDownloadRaw = 0;
        private List<TransLog> downTransLog = new List<TransLog>();
        private List<TransLog> upTransLog = new List<TransLog>();
        private long maxTransDownload = 0;
        private long maxTransUpload = 0;
        private int avgConnectTime = -1;
        //private List<TransLog> speedLog = null;
        private LinkedList<ErrorLog> errList = new LinkedList<ErrorLog>();

        private const int avgTime = 5;

        public ServerSpeedLog()
        {

        }

        public ServerSpeedLog(long upload, long download)
        {
            lock (this)
            {
                transUpload = upload;
                transDownload = download;
            }
        }

        public void ClearTrans()
        {
            lock (this)
            {
                transUpload = 0;
                transDownload = 0;
            }
        }

        public ServerSpeedLogShow Translate()
        {
            ServerSpeedLogShow ret = new ServerSpeedLogShow();
            lock (this)
            {
                Sweep();
                ret.avgDownloadBytes = AvgDownloadBytes;
                ret.avgUploadBytes = AvgUploadBytes;
                ret.avgConnectTime = AvgConnectTime;
                ret.maxDownloadBytes = maxTransDownload;
                ret.maxUploadBytes = maxTransUpload;
                ret.totalConnectTimes = totalConnectTimes;
                ret.totalDisconnectTimes = totalDisconnectTimes;
                ret.errorConnectTimes = errorConnectTimes;
                ret.errorTimeoutTimes = errorTimeoutTimes;
                ret.errorDecodeTimes = errorDecodeTimes;
                ret.errorEmptyTimes = errorEmptyTimes;
                ret.errorLogTimes = errList.Count;
                ret.errorContinurousTimes = errorContinurousTimes;
                ret.totalUploadBytes = transUpload;
                ret.totalDownloadBytes = transDownload;
                ret.totalDownloadRawBytes = transDownloadRaw;
            }
            return ret;
        }
        public long TotalConnectTimes
        {
            get
            {
                lock (this)
                {
                    return totalConnectTimes;
                }
            }
        }
        public long TotalDisconnectTimes
        {
            get
            {
                lock (this)
                {
                    return totalDisconnectTimes;
                }
            }
        }
        public long ErrorConnectTimes
        {
            get
            {
                lock (this)
                {
                    return errorConnectTimes;
                }
            }
        }
        public long ErrorTimeoutTimes
        {
            get
            {
                lock (this)
                {
                    return errorTimeoutTimes;
                }
            }
        }
        public long ErrorEncryptTimes
        {
            get
            {
                lock (this)
                {
                    return errorDecodeTimes;
                }
            }
        }
        public long ErrorContinurousTimes
        {
            get
            {
                lock (this)
                {
                    return errorContinurousTimes;
                }
            }
        }
        protected static long UpdateMaxTrans(long lastMaxTrans, List<TransLog> transAvgLog)
        {
            if (transAvgLog.Count > 1)
            {
                long avg_totalBytes = 0;
                double avg_totalTime = 0;
                for (int i = 0; i < transAvgLog.Count; ++i)
                {
                    avg_totalBytes += transAvgLog[i].size - transAvgLog[i].firstsize;
                    avg_totalTime += (transAvgLog[i].endTime - transAvgLog[i].recvTime).TotalSeconds;
                }
                return (long)(avg_totalBytes / avg_totalTime);
            }
            return lastMaxTrans;
        }
        public long AvgDownloadBytes
        {
            get
            {
                List<TransLog> transLog;
                lock (this)
                {
                    transLog = new List<TransLog>();
                    for (int i = 0; i < downTransLog.Count; ++i)
                    {
                        transLog.Add(downTransLog[i]);
                    }
                }
                {
                    long totalBytes = 0;
                    double totalTime = 0;
                    if (transLog.Count == 0 || transLog.Count > 0 && DateTime.Now > transLog[transLog.Count - 1].recvTime.AddSeconds(avgTime))
                    {
                        return 0;
                    }
                    for (int i = 0; i < transLog.Count; ++i)
                    {
                        totalBytes += transLog[i].size;
                    }
                    totalBytes -= transLog[0].firstsize;

                    if (transLog.Count > 1)
                        totalTime = (transLog[transLog.Count - 1].endTime - transLog[0].recvTime).TotalSeconds;
                    if (totalTime > 0.2)
                    {
                        long ret = (long)(totalBytes / totalTime);
                        return ret;
                    }
                    else
                        return 0;
                }
            }
        }
        public long AvgUploadBytes
        {
            get
            {
                List<TransLog> transLog;
                lock (this)
                {
                    if (this.upTransLog == null)
                        return 0;
                    transLog = new List<TransLog>();
                    for (int i = 0; i < upTransLog.Count; ++i)
                    {
                        transLog.Add(upTransLog[i]);
                    }
                }
                {
                    long totalBytes = 0;
                    double totalTime = 0;
                    if (transLog.Count == 0 || transLog.Count > 0 && DateTime.Now > transLog[transLog.Count - 1].recvTime.AddSeconds(avgTime))
                    {
                        return 0;
                    }
                    for (int i = 0; i < transLog.Count; ++i)
                    {
                        totalBytes += transLog[i].size;
                    }
                    totalBytes -= transLog[0].firstsize;

                    if (transLog.Count > 1)
                        totalTime = (transLog[transLog.Count - 1].endTime - transLog[0].recvTime).TotalSeconds;
                    if (totalTime > 0.2)
                    {
                        long ret = (long)(totalBytes / totalTime);
                        return ret;
                    }
                    else
                        return 0;
                }
            }
        }
        public long AvgConnectTime
        {
            get
            {
                return avgConnectTime;
            }
        }
        public void ClearError()
        {
            lock (this)
            {
                if (totalConnectTimes > totalDisconnectTimes)
                    totalConnectTimes -= totalDisconnectTimes;
                else
                    totalConnectTimes = 0;
                totalDisconnectTimes = 0;
                errorConnectTimes = 0;
                errorTimeoutTimes = 0;
                errorDecodeTimes = 0;
                errorEmptyTimes = 0;
                errList.Clear();
                errorContinurousTimes = 0;
            }
        }
        public void ClearMaxSpeed()
        {
            lock (this)
            {
                maxTransDownload = 0;
                maxTransUpload = 0;
            }
        }
        public void Clear()
        {
            lock (this)
            {
                if (totalConnectTimes > totalDisconnectTimes)
                    totalConnectTimes -= totalDisconnectTimes;
                else
                    totalConnectTimes = 0;
                totalDisconnectTimes = 0;
                errorConnectTimes = 0;
                errorTimeoutTimes = 0;
                errorDecodeTimes = 0;
                errorEmptyTimes = 0;
                errList.Clear();
                errorContinurousTimes = 0;
                transUpload = 0;
                transDownload = 0;
                transDownloadRaw = 0;
                maxTransDownload = 0;
                maxTransUpload = 0;
            }
        }
        public void AddConnectTimes()
        {
            lock (this)
            {
                totalConnectTimes += 1;
            }
        }
        public void AddDisconnectTimes()
        {
            lock (this)
            {
                totalDisconnectTimes += 1;
            }
        }
        protected void Sweep()
        {
            while (errList.Count > 0)
            {
                if ((DateTime.Now - errList.First.Value.time).TotalMinutes < 30 && errList.Count < 100)
                    break;

                int errCode = errList.First.Value.errno;
                errList.RemoveFirst();
                if (errCode == 1)
                {
                    if (errorConnectTimes > 0) errorConnectTimes -= 1;
                }
                else if (errCode == 2)
                {
                    if (errorTimeoutTimes > 0) errorTimeoutTimes -= 1;
                }
                else if (errCode == 3)
                {
                    if (errorDecodeTimes > 0) errorDecodeTimes -= 1;
                }
                else if (errCode == 4)
                {
                    if (errorEmptyTimes > 0) errorEmptyTimes -= 1;
                }
            }
        }
        public void AddNoErrorTimes()
        {
            lock (this)
            {
                errList.AddLast(new ErrorLog(0));
                errorEmptyTimes = 0;
                Sweep();
            }
        }
        public void AddErrorTimes()
        {
            lock (this)
            {
                errorConnectTimes += 1;
                errorContinurousTimes += 2;
                errList.AddLast(new ErrorLog(1));
                Sweep();
            }
        }
        public void AddTimeoutTimes()
        {
            lock (this)
            {
                errorTimeoutTimes += 1;
                errorContinurousTimes += 1;
                errList.AddLast(new ErrorLog(2));
                Sweep();
            }
        }
        public void AddErrorDecodeTimes()
        {
            lock (this)
            {
                errorDecodeTimes += 1;
                errorContinurousTimes += 10;
                errList.AddLast(new ErrorLog(3));
                Sweep();
            }
        }
        public void AddErrorEmptyTimes()
        {
            lock (this)
            {
                errorEmptyTimes += 1;
                errorContinurousTimes += 1;
                errList.AddLast(new ErrorLog(0));
                Sweep();
            }
        }
        protected static void UpdateTransLog(List<TransLog> transLog, int bytes, DateTime now, ref long maxTrans, bool updateMaxTrans)
        {
            if (transLog.Count > 0)
            {
                const int base_time_diff = 100;
                const int max_time_diff = 3 * base_time_diff;
                int time_diff = (int)(now - transLog[transLog.Count - 1].recvTime).TotalMilliseconds;
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

                        int base_times = 1 + (maxTrans > 1024 * 512 ? 1 : 0);
                        int last_index = (transLog.Count - 1) - 2;
                        if (updateMaxTrans && transLog.Count >= 6 && transLog[last_index].times > base_times)
                        {
                            int begin_index = last_index - 1;
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
                                TransLog t = new TransLog(transLog[begin_index].firstsize, transLog[begin_index].recvTime);
                                t.endTime = transLog[last_index].endTime;
                                t.size = 0;
                                for (int i = begin_index; i <= last_index; ++i)
                                {
                                    t.size += transLog[i].size;
                                }
                                if (maxTrans == 0)
                                {
                                    maxTrans = (long)((t.size - t.firstsize) / (t.endTime - t.recvTime).TotalSeconds * 0.7);
                                }
                                else
                                {
                                    double a = 2.0 / (1 + 32);
                                    maxTrans = (long)(0.5 + maxTrans * (1 - a) + a * ((t.size - t.firstsize) / (t.endTime - t.recvTime).TotalSeconds));
                                }
                            }
                        }
                    }
                    else
                    {
                        int i = transLog.Count - 1;
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
                while (transLog.Count > 0 && now > transLog[0].recvTime.AddSeconds(avgTime))
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
            lock (this)
            {
                transUpload += bytes;
                UpdateTransLog(upTransLog, bytes, now, ref maxTransUpload, updateMaxTrans);
            }
        }
        public void AddDownloadBytes(int bytes, DateTime now, bool updateMaxTrans)
        {
            lock (this)
            {
                transDownload += bytes;
                UpdateTransLog(downTransLog, bytes, now, ref maxTransDownload, updateMaxTrans);
            }
        }
        public void AddDownloadRawBytes(long bytes)
        {
            lock (this)
            {
                transDownloadRaw += bytes;
            }
        }
        public void ResetErrorDecodeTimes()
        {
            lock (this)
            {
                errorDecodeTimes = 0;
                errorEmptyTimes = 0;
                errorContinurousTimes = 0;
            }
        }
        public void ResetContinurousTimes()
        {
            lock (this)
            {
                errorEmptyTimes = 0;
                errorContinurousTimes = 0;
            }
        }
        public void ResetEmptyTimes()
        {
            lock (this)
            {
                errorEmptyTimes = 0;
            }
        }
        public void AddConnectTime(int millisecond)
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
                if (avgConnectTime == -1)
                {
                    avgConnectTime = millisecond;
                }
                else
                {
                    double a = 2.0 / (1 + 16);
                    avgConnectTime = (int)(0.5 + avgConnectTime * (1 - a) + a * millisecond);
                }
            }
        }
    }

}
