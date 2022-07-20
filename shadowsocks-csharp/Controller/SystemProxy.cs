using Shadowsocks.Controller.Service;
using Shadowsocks.Enums;
using Shadowsocks.Model;
using System;
using WindowsProxy;

namespace Shadowsocks.Controller;

public static class SystemProxy
{
    private static readonly ProxyStatus Old;
    static SystemProxy()
    {
        try
        {
            using var proxy = new ProxyService();
            Old = proxy.Query();
        }
        catch (Exception e)
        {
            Logging.LogUsefulException(e);
        }
    }

    public static void Restore()
    {
        try
        {
            using var proxy = new ProxyService();
            proxy.Set(Old);
        }
        catch (Exception e)
        {
            Logging.LogUsefulException(e);
        }
    }

    public static void Update(Configuration config, PACServer pacSrv)
    {
        var sysProxyMode = config.SysProxyMode;
        try
        {
            using var proxy = new ProxyService();

            switch (sysProxyMode)
            {
                case ProxyMode.Direct:
                {
                    proxy.Direct();
                    break;
                }
                case ProxyMode.Pac:
                {
                    proxy.AutoConfigUrl = pacSrv.PacUrl;
                    proxy.Pac();
                    break;
                }
                case ProxyMode.Global:
                {
                    proxy.Server = $@"localhost:{config.LocalPort}";
                    proxy.Bypass = string.Join(';', ProxyService.LanIp);
                    proxy.Global();
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Logging.LogUsefulException(e);
        }
    }
}
