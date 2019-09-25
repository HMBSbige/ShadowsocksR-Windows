namespace Shadowsocks.Model
{
    public enum ProxyRuleMode
    {
        Disable = 0,
        BypassLan,
        BypassLanAndChina,
        BypassLanAndNotChina,
        UserCustom = 16
    }
}