using System;
using System.Collections;
using System.Net;

namespace Shadowsocks.Model
{
    public class IPAddressCmp : IPAddress, IComparable
    {
        public IPAddressCmp(IPAddress ip) : base(ip.GetAddressBytes())
        {
        }

        public IPAddressCmp(byte[] ip) : base(ip)
        {
        }

        public IPAddressCmp(string ip) : base(FromString(ip).GetAddressBytes())
        {
        }

        public static IPAddress FromString(string ip)
        {
            TryParse(ip, out var addr);
            return addr;
        }

        public int CompareTo(object obj)
        {
            var b1 = GetAddressBytes();
            var b2 = ((IPAddressCmp)obj).GetAddressBytes();
            for (var i = 0; i < b1.Length; ++i)
            {
                if (b1[i] < b2[i])
                    return -1;
                if (b1[i] > b2[i])
                    return 1;
            }
            if (b1.Length < b2.Length)
                return -1;
            if (b1.Length > b2.Length)
                return 1;
            return 0;
        }

        public IPAddressCmp ToIPv6()
        {
            if (AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                return this;
            var b1 = GetAddressBytes();
            var br = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xff, 0xff, 0, 0, 0, 0 };
            b1.CopyTo(br, 12);
            return new IPAddressCmp(br);
        }

        public IPAddressCmp Inc()
        {
            var b = GetAddressBytes();
            var i = b.Length - 1;
            for (; i >= 0; --i)
            {
                if (b[i] == 0xff)
                {
                    b[i] = 0;
                }
                else
                {
                    b[i]++;
                    break;
                }
            }
            if (i < 0)
            {
                return new IPAddressCmp(GetAddressBytes());
            }
            return new IPAddressCmp(b);
        }
    }

    public class IPSegment
    {
        protected SortedList list = new SortedList();

        public IPSegment(object val = null)
        {
            list.Add(new IPAddressCmp(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }), val);
        }

        public bool insert(IPAddressCmp ipStart, IPAddressCmp ipEnd, object val)
        {
            var s = ipStart.ToIPv6();
            var e = ipEnd.ToIPv6().Inc();
            object ed_val = null;
            if (list.Contains(s))
            {
                ed_val = list[s];
                list[s] = val;
            }
            else
            {
                list[s] = val;
                var index = list.IndexOfKey(s) - 1;
                if (index >= 0)
                {
                    ed_val = list.GetByIndex(index);
                }
            }
            {
                var index = list.IndexOfKey(s);
                while (index > 0)
                {
                    if (val.Equals(list.GetByIndex(index - 1)))
                    {
                        list.RemoveAt(index);
                        --index;
                    }
                    else
                        break;
                }
                ++index;
                var keep = false;
                while (index < list.Count)
                {
                    var cmp = ((IPAddressCmp)list.GetKey(index)).CompareTo(e);
                    if (cmp >= 0)
                    {
                        if (cmp == 0)
                            keep = true;
                        break;
                    }
                    ed_val = list.GetByIndex(index);
                    list.RemoveAt(index);
                }
                if (!keep)
                {
                    list[e] = ed_val;
                    index = list.IndexOfKey(e);
                    while (index > 0)
                    {
                        if (ed_val != null && ed_val.Equals(list.GetByIndex(index - 1)))
                        {
                            list.RemoveAt(index);
                            --index;
                        }
                        else
                            break;
                    }
                    while (index + 1 < list.Count)
                    {
                        if (ed_val != null && ed_val.Equals(list.GetByIndex(index + 1)))
                        {
                            list.RemoveAt(index);
                        }
                        else
                            break;
                    }
                }
            }
            return true;
        }

        public object Get(IPAddressCmp ip)
        {
            var ip_addr = ip.ToIPv6();
            int l = 0, r = list.Count - 1;
            while (l < r)
            {
                var m = (l + r + 1) / 2;
                if (list.GetKey(m) is IPAddressCmp v)
                {
                    var cmp = v.CompareTo(ip_addr);

                    if (cmp > 0)
                    {
                        r = m - 1;
                    }
                    else if (cmp < 0)
                    {
                        l = m;
                    }
                    else if (cmp == 0)
                    {
                        return list[m];
                    }
                }
            }
            return list.GetByIndex(l);
        }
    }
}
