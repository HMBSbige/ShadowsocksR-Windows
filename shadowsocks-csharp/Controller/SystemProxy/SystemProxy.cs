using Shadowsocks.Controller.Service;
using Shadowsocks.Enums;
using Shadowsocks.Model;
using System;

namespace Shadowsocks.Controller.SystemProxy
{
    public static class SystemProxy
    {
        public static void Update(Configuration config, bool forceDisable, PACServer pacSrv)
        {
            var sysProxyMode = config.SysProxyMode;
            if (sysProxyMode == ProxyMode.NoModify)
            {
                return;
            }
            if (forceDisable)
            {
                sysProxyMode = ProxyMode.Direct;
            }
            var global = sysProxyMode == ProxyMode.Global;
            var enabled = sysProxyMode != ProxyMode.Direct;
            try
            {
                using var proxy = new SetSystemProxy();
                if (enabled)
                {
                    if (global)
                    {
                        proxy.Global($@"localhost:{config.LocalPort}");
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
