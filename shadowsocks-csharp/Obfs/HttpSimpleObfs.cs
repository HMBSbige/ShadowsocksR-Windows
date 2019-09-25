﻿using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Shadowsocks.Obfs
{
    class HttpSimpleObfs : ObfsBase
    {
        public HttpSimpleObfs(string method)
            : base(method)
        {
            has_sent_header = false;
            //has_recv_header = false;
            raw_trans_sent = false;
            raw_trans_recv = false;
        }
        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
                //modify original protocol, wrap protocol, obfs param
                {"http_simple", new[] {0, 1, 1}},
                {"http_post", new[] {0, 1, 1}},
                {"random_head", new[] {0, 1, 0}}
        };
        private static string[] _request_path = {
            "", "",
            "login.php?redir=", "",
            "register.php?code=", "",
            "?keyword=", "",
            "search?src=typd&q=", "&lang=en",
            "s?ie=utf-8&f=8&rsv_bp=1&rsv_idx=1&ch=&bar=&wd=", "&rn=",
            "post.php?id=", "&goto=view.php"
        };

        private static string[] _request_useragent = {
            "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:40.0) Gecko/20100101 Firefox/40.0",
            "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:40.0) Gecko/20100101 Firefox/44.0",
            "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/535.11 (KHTML, like Gecko) Ubuntu/11.10 Chromium/27.0.1453.93 Chrome/27.0.1453.93 Safari/537.36",
            "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:35.0) Gecko/20100101 Firefox/35.0",
            "Mozilla/5.0 (compatible; WOW64; MSIE 10.0; Windows NT 6.2)",
            "Mozilla/5.0 (Windows; U; Windows NT 6.1; en-US) AppleWebKit/533.20.25 (KHTML, like Gecko) Version/5.0.4 Safari/533.20.27",
            "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.3; Trident/7.0; .NET4.0E; .NET4.0C)",
            "Mozilla/5.0 (Windows NT 6.3; Trident/7.0; rv:11.0) like Gecko",
            "Mozilla/5.0 (Linux; Android 4.4; Nexus 5 Build/BuildID) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/30.0.0.0 Mobile Safari/537.36",
            "Mozilla/5.0 (iPad; CPU OS 5_0 like Mac OS X) AppleWebKit/534.46 (KHTML, like Gecko) Version/5.1 Mobile/9A334 Safari/7534.48.3",
            "Mozilla/5.0 (iPhone; CPU iPhone OS 5_0 like Mac OS X) AppleWebKit/534.46 (KHTML, like Gecko) Version/5.1 Mobile/9A334 Safari/7534.48.3"
        };
        private static int _useragent_index = new Random().Next(_request_useragent.Length);

        private bool has_sent_header;
        //private bool has_recv_header;
        private bool raw_trans_sent;
        private bool raw_trans_recv;
        private List<byte[]> data_buffer = new List<byte[]>();
        private Random random = new Random();

        public static List<string> SupportedObfs()
        {
            return new List<string>(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        private string data2urlencode(byte[] encryptdata, int datalength)
        {
            var ret = "";
            for (var i = 0; i < datalength; ++i)
            {
                ret += "%" + encryptdata[i].ToString("x2");
            }
            return ret;
        }

        private string boundary()
        {
            var set = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var ret = "";
            for (var i = 0; i < 32; ++i)
            {
                ret += set[random.Next(set.Length)];
            }
            return ret;
        }

        public override byte[] ClientEncode(byte[] encryptdata, int datalength, out int outlength)
        {
            if (raw_trans_sent)
            {
                SentLength += datalength;
                outlength = datalength;
                return encryptdata;
            }

            var outdata = new byte[datalength + 4096];
            byte[] headdata;
            if (Method == "random_head")
            {
                if (has_sent_header)
                {
                    outlength = 0;
                    if (datalength > 0)
                    {
                        var data = new byte[datalength];
                        Array.Copy(encryptdata, 0, data, 0, datalength);
                        data_buffer.Add(data);
                    }
                    else
                    {
                        foreach (var data in data_buffer)
                        {
                            Array.Copy(data, 0, outdata, outlength, data.Length);
                            SentLength += data.Length;
                            outlength += data.Length;
                        }
                        data_buffer.Clear();
                        raw_trans_sent = true;
                    }
                }
                else
                {
                    var size = random.Next(96) + 8;
                    var rnd = new byte[size];
                    random.NextBytes(rnd);
                    Util.CRC32.SetCRC32(rnd);
                    rnd.CopyTo(outdata, 0);
                    outlength = rnd.Length;

                    var data = new byte[datalength];
                    Array.Copy(encryptdata, 0, data, 0, datalength);
                    data_buffer.Add(data);
                }
            }
            else if (Method == "http_simple" || Method == "http_post")
            {
                var headsize = Server.Iv.Length + Server.head_len;
                if (datalength - headsize > 64)
                {
                    headdata = new byte[headsize + random.Next(0, 64)];
                }
                else
                {
                    headdata = new byte[datalength];
                }
                Array.Copy(encryptdata, 0, headdata, 0, headdata.Length);
                var request_path_index = new Random().Next(_request_path.Length / 2) * 2;
                var host = Server.host;
                var custom_head = "";
                if (Server.param.Length > 0)
                {
                    var custom_heads = Server.param.Split(new[] { '#' }, 2);
                    var param = Server.param;
                    if (custom_heads.Length > 1)
                    {
                        custom_head = custom_heads[1];
                        param = custom_heads[0];
                    }
                    var hosts = param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (hosts.Length > 0)
                    {
                        host = hosts[random.Next(hosts.Length)];
                        host = host.Trim(' ');
                    }
                }
                var http_buf =
                        (Method == "http_post" ? "POST /" : "GET /") + _request_path[request_path_index] + data2urlencode(headdata, headdata.Length) + _request_path[request_path_index + 1] + " HTTP/1.1\r\n"
                        + "Host: " + host + (Server.port == 80 ? "" : ":" + Server.port) + "\r\n";
                if (custom_head.Length > 0)
                {
                    http_buf += custom_head.Replace("\\n", "\r\n") + "\r\n\r\n";
                }
                else
                {
                    http_buf +=
                            "User-Agent: " + _request_useragent[_useragent_index] + "\r\n"
                            + "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8\r\n"
                            + "Accept-Language: en-US,en;q=0.8\r\n"
                            + "Accept-Encoding: gzip, deflate\r\n"
                            + (Method == "http_post" ? "Content-Type: multipart/form-data; boundary=" + boundary() + "\r\n" : "")
                            + "DNT: 1\r\n"
                            + "Connection: keep-alive\r\n"
                            + "\r\n";
                }
                for (var i = 0; i < http_buf.Length; ++i)
                {
                    outdata[i] = (byte)http_buf[i];
                }
                if (headdata.Length < datalength)
                {
                    Array.Copy(encryptdata, headdata.Length, outdata, http_buf.Length, datalength - headdata.Length);
                }
                SentLength += headdata.Length;
                outlength = http_buf.Length + datalength - headdata.Length;
                raw_trans_sent = true;
            }
            else
            {
                outlength = 0;
            }
            has_sent_header = true;
            return outdata;
        }

        private int FindSubArray(byte[] array, int length, byte[] subArray)
        {
            for (var pos = 0; pos < length; ++pos)
            {
                var offset = 0;
                for (; offset < subArray.Length; ++offset)
                {
                    if (array[pos + offset] != subArray[offset])
                        break;
                }
                if (offset == subArray.Length)
                {
                    return pos;
                }
            }
            return -1;
        }

        public override byte[] ClientDecode(byte[] encryptdata, int datalength, out int outlength, out bool needsendback)
        {
            if (raw_trans_recv)
            {
                outlength = datalength;
                needsendback = false;
                return encryptdata;
            }

            var outdata = new byte[datalength];
            if (Method == "random_head")
            {
                outlength = 0;
                raw_trans_recv = true;
                needsendback = true;
                return encryptdata;
            }

            var pos = FindSubArray(encryptdata, datalength, new[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' });
            if (pos > 0)
            {
                outlength = datalength - (pos + 4);
                Array.Copy(encryptdata, pos + 4, outdata, 0, outlength);
                raw_trans_recv = true;
            }
            else
            {
                outlength = 0;
            }
            needsendback = false;
            return outdata;
        }
    }
    public class TlsAuthData
    {
        public byte[] clientID;
        public Dictionary<string, byte[]> ticket_buf;
    }

    class TlsTicketAuthObfs : ObfsBase
    {
        public TlsTicketAuthObfs(string method)
            : base(method)
        {
            handshake_status = 0;
            if (method == "tls1.2_ticket_fastauth")
                fastauth = true;
        }
        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
                {"tls1.2_ticket_auth", new[]  {0, 1, 1}},
                {"tls1.2_ticket_fastauth", new[]  {0, 1, 1}}
        };

        private int handshake_status;
        private List<byte[]> data_sent_buffer = new List<byte[]>();
        private byte[] data_recv_buffer = new byte[0];
        private uint send_id;
        private bool fastauth;

        protected static RNGCryptoServiceProvider g_random = new RNGCryptoServiceProvider();
        protected Random random = new Random();
        protected const int overhead = 5;

        public static List<string> SupportedObfs()
        {
            return new List<string>(_obfs.Keys);
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
            var key = new byte[Server.key.Length + 32];
            Server.key.CopyTo(key, 0);
            ((TlsAuthData)Server.data).clientID.CopyTo(key, Server.key.Length);

            var sha1 = new HMACSHA1(key);
            var sha1data = sha1.ComputeHash(data, 0, length - 10);

            Array.Copy(sha1data, 0, data, length - 10, 10);
        }

        public void PackAuthData(byte[] outdata)
        {
            var authData = (TlsAuthData)Server.data;
            var outlength = 32;
            {
                var randomdata = new byte[18];
                lock (authData)
                {
                    g_random.GetBytes(randomdata);
                }
                randomdata.CopyTo(outdata, 4);
            }

            lock (authData)
            {
                if (authData.clientID == null)
                {
                    authData.clientID = new byte[32];
                    g_random.GetBytes(authData.clientID);
                }
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
                    if (len > datalength - start) len = datalength - start;
                    PackData(encryptdata, ref start, len, outdata, ref outlength);
                }
                while (datalength - start > 2048)
                {
                    var len = random.Next(4096) + 100;
                    if (len > datalength - start) len = datalength - start;
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
                            var ticket_size = random.Next(32, 196) * 2;
                            ticket = new byte[ticket_size];
                            g_random.GetBytes(ticket);
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
                        throw new ObfsException("ClientDecode appdata error");
                    var len = (data_recv_buffer[3] << 8) + data_recv_buffer[4];
                    var pack_len = len + 5;
                    if (pack_len > data_recv_buffer.Length)
                        break;
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

                    if (!Util.Utils.BitCompare(data_recv_buffer, 11 + 22, data, 22, 10))
                    {
                        throw new ObfsException("ClientDecode data error: wrong sha1");
                    }

                    var headerlength = data_recv_buffer.Length;
                    data = new byte[headerlength];
                    Array.Copy(data_recv_buffer, 0, data, 0, headerlength - 10);
                    hmac_sha1(data, headerlength);
                    if (!Util.Utils.BitCompare(data_recv_buffer, headerlength - 10, data, headerlength - 10, 10))
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

                        if (!Util.Utils.BitCompare(data_recv_buffer, headerlength - 10, data, headerlength - 10, 10))
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
