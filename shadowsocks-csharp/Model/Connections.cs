using System.Collections.Generic;
using Shadowsocks.Proxy;

namespace Shadowsocks.Model
{
    public class Connections
    {
        private readonly Dictionary<IHandler, int> sockets = new Dictionary<IHandler, int>();
        public bool AddRef(IHandler socket)
        {
            lock (this)
            {
                if (sockets.ContainsKey(socket))
                {
                    sockets[socket] += 1;
                }
                else
                {
                    sockets[socket] = 1;
                }
                return true;
            }
        }
        public bool DecRef(IHandler socket)
        {
            lock (this)
            {
                if (sockets.ContainsKey(socket))
                {
                    sockets[socket] -= 1;
                    if (sockets[socket] == 0)
                    {
                        sockets.Remove(socket);
                    }
                }
                else
                {
                    return false;
                }
                return true;
            }
        }
        public void CloseAll()
        {
            IHandler[] s;
            lock (this)
            {
                s = new IHandler[sockets.Count];
                sockets.Keys.CopyTo(s, 0);
            }
            foreach (IHandler handler in s)
            {
                try
                {
                    handler.Shutdown();
                }
                catch
                {

                }
            }
        }
        public int Count
        {
            get
            {
                lock (this)
                {
                    return sockets.Count;
                }
            }
        }
    }
}