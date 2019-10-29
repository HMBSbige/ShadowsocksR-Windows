using Shadowsocks.Properties;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Shadowsocks.Model
{
    public class IPRangeSet
    {
        private const string ApnicFilename = @"delegated-apnic-latest";
        private const string ApnicExtFilename = @"delegated-apnic-extended-latest";
        public const string ChnFilename = @"chn_ip.txt";
        private readonly uint[] _set = new uint[256 * 256 * 8];

        private void InsertRange(uint begin, uint end)
        {
            begin /= 256;
            end /= 256;
            for (var i = begin; i <= end; ++i)
            {
                var pos = i / 32;
                var mv = (int)(i & 31);
                _set[pos] |= 1u << mv;
            }
        }

        private void Insert(uint begin, uint size)
        {
            InsertRange(begin, begin + size - 1);
        }

        private void Insert(IPAddress addr, uint size)
        {
            var bytes_addr = addr.GetAddressBytes();
            Array.Reverse(bytes_addr);
            Insert(BitConverter.ToUInt32(bytes_addr, 0), size);
        }

        private void Insert(IPAddress addr_beg, IPAddress addr_end)
        {
            var bytes_addr_beg = addr_beg.GetAddressBytes();
            Array.Reverse(bytes_addr_beg);
            var bytes_addr_end = addr_end.GetAddressBytes();
            Array.Reverse(bytes_addr_end);
            InsertRange(BitConverter.ToUInt32(bytes_addr_beg, 0), BitConverter.ToUInt32(bytes_addr_end, 0));
        }

        private bool IsIn(uint ip)
        {
            ip /= 256;
            var pos = ip / 32;
            var mv = (int)(ip & 31);
            return (_set[pos] & (1u << mv)) != 0;
        }

        public bool IsInIPRange(IPAddress addr)
        {
            var bytes_addr = addr.GetAddressBytes();
            Array.Reverse(bytes_addr);
            return IsIn(BitConverter.ToUInt32(bytes_addr, 0));
        }

        private bool LoadApnic(string zone)
        {
            var filename = ApnicExtFilename;
            var absFilePath = Path.Combine(Directory.GetCurrentDirectory(), filename);
            if (!File.Exists(absFilePath))
            {
                filename = ApnicFilename;
                absFilePath = Path.Combine(Directory.GetCurrentDirectory(), filename);
            }
            if (File.Exists(absFilePath))
            {
                try
                {
                    using (var stream = File.OpenText(absFilePath))
                    {
                        using var out_stream = new StreamWriter(File.OpenWrite(ChnFilename));
                        while (true)
                        {
                            var line = stream.ReadLine();
                            if (line == null)
                                break;
                            var parts = line.Split('|');
                            if (parts.Length < 7)
                                continue;
                            if (parts[0] != "apnic" || parts[1] != zone || parts[2] != "ipv4")
                                continue;
                            IPAddress.TryParse(parts[3], out var addr);
                            var size = uint.Parse(parts[4]);
                            Insert(addr, size);

                            var addr_bytes = addr.GetAddressBytes();
                            Array.Reverse(addr_bytes);
                            var ip_addr = BitConverter.ToUInt32(addr_bytes, 0);
                            ip_addr += size - 1;
                            addr_bytes = BitConverter.GetBytes(ip_addr);
                            Array.Reverse(addr_bytes);
                            out_stream.Write(parts[3] + " " + new IPAddress(addr_bytes) + "\r\n");
                        }
                    }
                    return true;
                }
                catch
                {
                    // ignored
                }
            }
            return false;
        }

        private bool LoadChn(IEnumerable<string> lines)
        {
            try
            {
                foreach (var line in lines)
                {
                    if (line == null)
                    {
                        break;
                    }
                    var parts = line.Split(' ');
                    if (parts.Length < 2)
                    {
                        continue;
                    }

                    IPAddress.TryParse(parts[0], out var addr_begin);
                    IPAddress.TryParse(parts[1], out var addr_end);
                    Insert(addr_begin, addr_end);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsEmpty()
        {
            return _set.All(unit => unit == 0u);
        }

        private void Clear()
        {
            Array.Clear(_set, 0, _set.Length);
        }

        public bool LoadChn()
        {
            var absFilePath = Path.Combine(Directory.GetCurrentDirectory(), ChnFilename);
            if (File.Exists(absFilePath))
            {
                if (!LoadChn(File.ReadLines(absFilePath, Encoding.UTF8)))
                {
                    Clear();
                }
            }
            if (IsEmpty())
            {
                if (!LoadApnic(@"CN"))
                {
                    Clear();
                }
            }
            if (IsEmpty())
            {
                return LoadChn(Resources.chn_ip.GetLines());
            }
            return true;
        }

        public void Reverse()
        {
            IPAddress.TryParse(@"240.0.0.0", out var addr_beg);
            IPAddress.TryParse(@"255.255.255.255", out var addr_end);
            Insert(addr_beg, addr_end);
            for (uint i = 0; i < _set.Length; ++i)
            {
                _set[i] = ~_set[i];
            }
        }
    }
}
