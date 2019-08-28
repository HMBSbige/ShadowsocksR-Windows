using Shadowsocks.Controller;
using Shadowsocks.Model;
using System;

namespace Shadowsocks.Proxy.SystemProxy
{
    public static class SystemProxy
    {
        public static void Update(Configuration config, bool forceDisable, PACServer pacSrv)
        {
            var sysProxyMode = config.sysProxyMode;
            if (sysProxyMode == (int)ProxyMode.NoModify)
            {
                return;
            }
            if (forceDisable)
            {
                sysProxyMode = (int)ProxyMode.Direct;
            }
            var global = sysProxyMode == (int)ProxyMode.Global;
            var enabled = sysProxyMode != (int)ProxyMode.Direct;
            using var proxy = new SetSystemProxy();
            try
            {
                if (enabled)
                {
                    if (global)
                    {
                        proxy.Global($@"localhost:{config.localPort}");
                    }
                    else
                    {
                        proxy.Pac(pacSrv.PacUrl);
                    }
                }
                else
                {
                    proxy.Direct();
                }
            }
            catch (Exception e)
            {
                Logging.LogUsefulException(e);
            }
        }
    }
}
