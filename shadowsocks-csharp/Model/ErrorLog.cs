using System;

namespace Shadowsocks.Model
{
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
}
