using System;

namespace Shadowsocks.Model.Transfer
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
}
