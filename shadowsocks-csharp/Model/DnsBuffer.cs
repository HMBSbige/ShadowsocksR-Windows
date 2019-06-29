using System;
using System.Net;

namespace Shadowsocks.Model
{
    public class DnsBuffer
    {
        public IPAddress ip;
        public DateTime updateTime;
        public string host;
        public bool force_expired;
        public bool isExpired(string host)
        {
            if (updateTime == null) return true;
            if (this.host != host) return true;
            if (force_expired && (DateTime.Now - updateTime).TotalMinutes > 1) return true;
            return (DateTime.Now - updateTime).TotalMinutes > 30;
        }
        public void UpdateDns(string host, IPAddress ip)
        {
            updateTime = DateTime.Now;
            this.ip = new IPAddress(ip.GetAddressBytes());
            this.host = host;
            force_expired = false;
        }
    }
}