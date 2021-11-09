using CryptoBase.Digests;
using CryptoBase.Macs.Hmac;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Shadowsocks.Obfs
{
    class TlsTicketAuthObfs : ObfsBase
    {
        public TlsTicketAuthObfs(string method)
                : base(method)
        {
            handshake_status = 0;
            if (method == "tls1.2_ticket_fastauth")
            {
                fastauth = true;
            }
        }
        private static Dictionary<string, int[]> _obfs = new()
        {
            { "tls1.2_ticket_auth", new[] { 0, 1, 1 } },
            { "tls1.2_ticket_fastauth", new[] { 0, 1, 1 } }
        };

        private int handshake_status;
        private List<byte[]> data_sent_buffer = new();
        private byte[] data_recv_buffer = new byte[0];
        private uint send_id;
        private bool fastauth;

        protected Random random = new();
        protected const int overhead = 5;

        public static List<string> SupportedObfs()
        {
            return new(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        public override object InitData()
        {
            return new TlsAuthData();
        }

        public override bool isAlwaysSendback()
        {
            return true;
        }

        public override int GetOverhead()
        {
            return overhead;
        }

        protected byte[] sni(string url)
        {
            if (url == null)
            {
                url = "";
            }
            var b_url = System.Text.Encoding.UTF8.GetBytes(url);
            var len = b_url.Length;
            var ret = new byte[len + 9];
            Array.Copy(b_url, 0, ret, 9, len);
            ret[7] = (byte)(len >> 8);
            ret[8] = (byte)len;
            len += 3;
            ret[4] = (byte)(len >> 8);
            ret[5] = (byte)len;
            len += 2;
            ret[2] = (byte)(len >> 8);
            ret[3] = (byte)len;
            return ret;
        }

        protected byte to_val(char c)
        {
            if (c > '9')
            {
                return (byte)(c - 'a' + 10);
            }

            return (byte)(c - '0');
        }

        protected byte[] to_bin(string str)
        {
            var ret = new byte[str.Length / 2];
            for (var i = 0; i < str.Length; i += 2)
            {
                ret[i / 2] = (byte)((to_val(str[i]) << 4) | to_val(str[i + 1]));
            }
            return ret;
        }

        protected void hmac_sha1(byte[] data, int length)
        {
            Span<byte> key = new byte[Server.key.Length + 32];
            Server.key.AsSpan().CopyTo(key);
            ((TlsAuthData)Server.data).clientID.AsSpan().CopyTo(key[Server.key.Length..]);

            using var hmac = HmacUtils.Create(DigestType.Sha1, key);
            hmac.Update(data.AsSpan(0, length - 10));
            Span<byte> hash = stackalloc byte[hmac.Length];
            hmac.GetMac(hash);
            hash[..10].CopyTo(data.AsSpan(length - 10, 10));
        }

        public void PackAuthData(byte[] outdata)
        {
            var authData = (TlsAuthData)Server.data;
            var outlength = 32;
            {
                var randomdata = new byte[18];
                lock (authData)
                {
                    RandomNumberGenerator.Fill(randomdata);
                }
                randomdata.CopyTo(outdata, 4);
            }

            lock (authData)
            {
                authData.clientID ??= RandomNumberGenerator.GetBytes(32);
            }
            var utc_time_second = (ulong)Math.Floor(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);
            var utc_time = (uint)utc_time_second;
            var time_bytes = BitConverter.GetBytes(utc_time);
            Array.Reverse(time_bytes);
            Array.Copy(time_bytes, 0, outdata, 0, 4);

            hmac_sha1(outdata, outlength);
        }

        protected void PackData(byte[] data, ref int start, int len, byte[] outdata, ref int outlength)
        {
            outdata[outlength] = 0x17;
            outdata[outlength + 1] = 0x3;
            outdata[outlength + 2] = 0x3;
            outdata[outlength + 3] = (byte)(len >> 8);
            outdata[outlength + 4] = (byte)len;
            Array.Copy(data, start, outdata, outlength + 5, len);
            start += len;
            outlength += len + 5;
            ++send_id;
        }

        public override byte[] ClientEncode(byte[] encryptdata, int datalength, out int outlength)
        {
            if (handshake_status == -1)
            {
                SentLength += datalength;
                outlength = datalength;
                return encryptdata;
            }
            var outdata = new byte[datalength + 4096];
            if ((handshake_status & 4) == 4)
            {
                var start = 0;
                outlength = 0;
                while (send_id <= 4 && datalength - start > 256)
                {
                    var len = random.Next(512) + 64;
                    if (len > datalength - start)
                    {
                        len = datalength - start;
                    }

                    PackData(encryptdata, ref start, len, outdata, ref outlength);
                }
                while (datalength - start > 2048)
                {
                    var len = random.Next(4096) + 100;
                    if (len > datalength - start)
                    {
                        len = datalength - start;
                    }

                    PackData(encryptdata, ref start, len, outdata, ref outlength);
                }
                if (datalength - start > 0)
                {
                    PackData(encryptdata, ref start, datalength - start, outdata, ref outlength);
                }
                return outdata;
            }
            if (datalength > 0)
            {
                var data = new byte[datalength + 5];
                data[0] = 0x17;
                data[1] = 0x3;
                data[2] = 0x3;
                data[3] = (byte)(datalength >> 8);
                data[4] = (byte)datalength;
                Array.Copy(encryptdata, 0, data, 5, datalength);
                data_sent_buffer.Add(data);
            }
            if ((handshake_status & 3) != 0)
            {
                outlength = 0;
                if ((handshake_status & 2) == 0)
                {
                    int[] finish_len_set = { 32 }; //, 40, 64
                    var finish_len = finish_len_set[random.Next(finish_len_set.Length)];
                    var hmac_data = new byte[11 + finish_len];
                    var rnd = new byte[finish_len - 10];
                    random.NextBytes(rnd);

                    var handshake_finish = System.Text.Encoding.ASCII.GetBytes("\x14\x03\x03\x00\x01\x01" + "\x16\x03\x03\x00\x20");
                    handshake_finish[handshake_finish.Length - 1] = (byte)finish_len;
                    handshake_finish.CopyTo(hmac_data, 0);
                    rnd.CopyTo(hmac_data, handshake_finish.Length);

                    hmac_sha1(hmac_data, hmac_data.Length);

                    data_sent_buffer.Insert(0, hmac_data);
                    SentLength -= hmac_data.Length;
                    handshake_status |= 2;
                }
                if (datalength == 0 || fastauth)
                {
                    foreach (var data in data_sent_buffer)
                    {
                        Util.Utils.SetArrayMinSize2(ref outdata, outlength + data.Length);
                        Array.Copy(data, 0, outdata, outlength, data.Length);
                        SentLength += data.Length;
                        outlength += data.Length;
                    }
                    data_sent_buffer.Clear();
                }
                if (datalength == 0)
                {
                    handshake_status |= 4;
                }
            }
            else
            {
                var rnd = new byte[32];
                PackAuthData(rnd);
                var ssl_buf = new List<byte>();
                var ext_buf = new List<byte>();
                var str_buf = "001cc02bc02fcca9cca8cc14cc13c00ac014c009c013009c0035002f000a0100";
                ssl_buf.AddRange(rnd);
                ssl_buf.Add(32);
                ssl_buf.AddRange(((TlsAuthData)Server.data).clientID);
                ssl_buf.AddRange(to_bin(str_buf));

                str_buf = "ff01000100";
                ext_buf.AddRange(to_bin(str_buf));
                var host = Server.host;
                if (Server.param.Length > 0)
                {
                    var hosts = Server.param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (hosts.Length > 0)
                    {
                        host = hosts[random.Next(hosts.Length)];
                        host = host.Trim(' ');
                    }
                }
                if (!string.IsNullOrEmpty(host) && host[host.Length - 1] >= '0' && host[host.Length - 1] <= '9' && Server.param.Length == 0)
                {
                    host = "";
                }
                ext_buf.AddRange(sni(host));
                var str_buf2 = "001700000023";
                ext_buf.AddRange(to_bin(str_buf2));
                {
                    var authData = (TlsAuthData)Server.data;
                    byte[] ticket;
                    lock (authData)
                    {
                        if (authData.ticket_buf == null)
                        {
                            authData.ticket_buf = new Dictionary<string, byte[]>();
                        }
                        if (!authData.ticket_buf.ContainsKey(host ?? throw new InvalidOperationException()) || random.Next(16) == 0)
                        {
                            var ticketSize = random.Next(32, 196) * 2;
                            ticket = RandomNumberGenerator.GetBytes(ticketSize);
                            authData.ticket_buf[host] = ticket;
                        }
                        else
                        {
                            ticket = authData.ticket_buf[host];
                        }
                    }
                    ext_buf.Add((byte)(ticket.Length >> 8));
                    ext_buf.Add((byte)(ticket.Length & 0xff));
                    ext_buf.AddRange(ticket);
                }
                var str_buf3 = "000d0016001406010603050105030401040303010303020102030005000501000000000012000075500000000b00020100000a0006000400170018";
                str_buf3 += "00150066000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";
                ext_buf.AddRange(to_bin(str_buf3));
                ext_buf.Insert(0, (byte)(ext_buf.Count % 256));
                ext_buf.Insert(0, (byte)((ext_buf.Count - 1) / 256));

                ssl_buf.AddRange(ext_buf);
                // client version
                ssl_buf.Insert(0, 3); // version
                ssl_buf.Insert(0, 3);
                // length
                ssl_buf.Insert(0, (byte)(ssl_buf.Count % 256));
                ssl_buf.Insert(0, (byte)((ssl_buf.Count - 1) / 256));
                ssl_buf.Insert(0, 0);
                ssl_buf.Insert(0, 1); // client hello
                // length
                ssl_buf.Insert(0, (byte)(ssl_buf.Count % 256));
                ssl_buf.Insert(0, (byte)((ssl_buf.Count - 1) / 256));
                //
                ssl_buf.Insert(0, 0x1); // version
                ssl_buf.Insert(0, 0x3);
                ssl_buf.Insert(0, 0x16);
                for (var i = 0; i < ssl_buf.Count; ++i)
                {
                    outdata[i] = ssl_buf[i];
                }
                outlength = ssl_buf.Count;

                handshake_status = 1;
            }
            return outdata;
        }

        public override byte[] ClientDecode(byte[] encryptdata, int datalength, out int outlength, out bool needsendback)
        {
            if (handshake_status == -1)
            {
                outlength = datalength;
                needsendback = false;
                return encryptdata;
            }

            if ((handshake_status & 8) == 8)
            {
                Array.Resize(ref data_recv_buffer, data_recv_buffer.Length + datalength);
                Array.Copy(encryptdata, 0, data_recv_buffer, data_recv_buffer.Length - datalength, datalength);
                needsendback = false;
                var outdata = new byte[65536];
                outlength = 0;
                while (data_recv_buffer.Length > 5)
                {
                    if (data_recv_buffer[0] != 0x17)
                    {
                        throw new ObfsException("ClientDecode appdata error");
                    }

                    var len = (data_recv_buffer[3] << 8) + data_recv_buffer[4];
                    var pack_len = len + 5;
                    if (pack_len > data_recv_buffer.Length)
                    {
                        break;
                    }

                    Array.Copy(data_recv_buffer, 5, outdata, outlength, len);
                    outlength += len;
                    var buffer = new byte[data_recv_buffer.Length - pack_len];
                    Array.Copy(data_recv_buffer, pack_len, buffer, 0, buffer.Length);
                    data_recv_buffer = buffer;
                }
                return outdata;
            }
            else
            {
                Array.Resize(ref data_recv_buffer, data_recv_buffer.Length + datalength);
                Array.Copy(encryptdata, 0, data_recv_buffer, data_recv_buffer.Length - datalength, datalength);
                outlength = 0;
                needsendback = false;
                if (data_recv_buffer.Length >= 11 + 32 + 1 + 32)
                {
                    var data = new byte[32];
                    Array.Copy(data_recv_buffer, 11, data, 0, 22);
                    hmac_sha1(data, data.Length);

                    if (!data_recv_buffer.AsSpan(11 + 22, 10).SequenceEqual(data.AsSpan(22, 10)))
                    {
                        throw new ObfsException("ClientDecode data error: wrong sha1");
                    }

                    var headerlength = data_recv_buffer.Length;
                    data = new byte[headerlength];
                    Array.Copy(data_recv_buffer, 0, data, 0, headerlength - 10);
                    hmac_sha1(data, headerlength);
                    if (!data_recv_buffer.AsSpan(headerlength - 10, 10).SequenceEqual(data.AsSpan(headerlength - 10, 10)))
                    {
                        headerlength = 0;
                        while (headerlength < data_recv_buffer.Length &&
                               (data_recv_buffer[headerlength] == 0x14 || data_recv_buffer[headerlength] == 0x16))
                        {
                            headerlength += 5;
                            if (headerlength >= data_recv_buffer.Length)
                            {
                                return encryptdata;
                            }
                            headerlength += (data_recv_buffer[headerlength - 2] << 8) | data_recv_buffer[headerlength - 1];
                            if (headerlength > data_recv_buffer.Length)
                            {
                                return encryptdata;
                            }
                        }
                        data = new byte[headerlength];
                        Array.Copy(data_recv_buffer, 0, data, 0, headerlength - 10);
                        hmac_sha1(data, headerlength);

                        if (!data_recv_buffer.AsSpan(headerlength - 10, 10).SequenceEqual(data.AsSpan(headerlength - 10, 10)))
                        {
                            throw new ObfsException("ClientDecode data error: wrong sha1");
                        }
                    }
                    var buffer = new byte[data_recv_buffer.Length - headerlength];
                    Array.Copy(data_recv_buffer, headerlength, buffer, 0, buffer.Length);
                    data_recv_buffer = buffer;
                    handshake_status |= 8;
                    var ret = ClientDecode(encryptdata, 0, out outlength, out needsendback);
                    needsendback = true;
                    return ret;
                }
                return encryptdata;
            }
        }
    }
}
