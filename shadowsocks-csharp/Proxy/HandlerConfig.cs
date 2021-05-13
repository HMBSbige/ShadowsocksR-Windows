using Shadowsocks.Enums;
using System;

namespace Shadowsocks.Proxy
{
    class HandlerConfig : ICloneable
    {
        public string TargetHost;
        public int TargetPort;

        public double Ttl; // Second
        public double ConnectTimeout;
        public int TryKeepAlive;
        public bool ForceLocalDnsQuery;
        // Server proxy
        public ProxyType ProxyType;
        public string Socks5RemoteHost;
        public int Socks5RemotePort;
        public string Socks5RemoteUsername;
        public string Socks5RemotePassword;
        public string ProxyUserAgent;
        // auto ban
        public bool AutoSwitchOff = true;
        // Reconnect
        public int ReconnectTimesRemain;
        public int ReconnectTimes;
        public bool Random;
        public bool ForceRandom;

        public object Clone()
        {
            var obj = new HandlerConfig
            {
                TargetHost = TargetHost,
                TargetPort = TargetPort,
                Ttl = Ttl,
                ConnectTimeout = ConnectTimeout,
                TryKeepAlive = TryKeepAlive,
                ForceLocalDnsQuery = ForceLocalDnsQuery,
                ProxyType = ProxyType,
                Socks5RemoteHost = Socks5RemoteHost,
                Socks5RemotePort = Socks5RemotePort,
                Socks5RemoteUsername = Socks5RemoteUsername,
                Socks5RemotePassword = Socks5RemotePassword,
                ProxyUserAgent = ProxyUserAgent,
                AutoSwitchOff = AutoSwitchOff,
                ReconnectTimesRemain = ReconnectTimesRemain,
                ReconnectTimes = ReconnectTimes,
                Random = Random,
                ForceRandom = ForceRandom
            };
            return obj;
        }
    }
}
