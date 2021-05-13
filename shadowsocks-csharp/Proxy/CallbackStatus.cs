namespace Shadowsocks.Proxy
{
    class CallbackStatus
    {
        protected int status;

        public CallbackStatus()
        {
            status = 0;
        }

        public void SetIfEqu(int newStatus, int oldStatus)
        {
            lock (this)
            {
                if (status == oldStatus)
                {
                    status = newStatus;
                }
            }
        }

        public int Status
        {
            get
            {
                lock (this)
                {
                    return status;
                }
            }
            set
            {
                lock (this)
                {
                    status = value;
                }
            }
        }
    }
}
