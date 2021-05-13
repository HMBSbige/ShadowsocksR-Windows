using System;
using System.Net;

namespace Shadowsocks.Model
{
    public class DnsBuffer
    {
        public IPAddress Ip;
        public DateTime updateTime;
        public string Host;
        public bool force_expired;

        public bool IsExpired(string host)
        {
            if (Host != host)
            {
                return true;
            }

            if (force_expired && (DateTime.Now - updateTime).TotalMinutes > 1)
            {
                return true;
            }

            return (DateTime.Now - updateTime).TotalMinutes > 30;
        }

        public void UpdateDns(string host, IPAddress ip)
        {
            updateTime = DateTime.Now;
            Ip = new IPAddress(ip.GetAddressBytes());
            Host = host;
            force_expired = false;
        }
    }
}
