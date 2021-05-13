using System;
using System.Net.Sockets;

namespace Shadowsocks.Util.NetUtils
{
    public static class SocketUtil
    {
        public static void FullClose(this Socket s)
        {
            try
            {
                s.Shutdown(SocketShutdown.Both);
            }
            catch (Exception)
            {
                // ignored
            }

            try
            {
                s.Disconnect(false);
            }
            catch (Exception)
            {
                // ignored
            }

            try
            {
                s.Close();
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}
