using System;
using System.Collections.Generic;

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
        private static Dictionary<string, int[]> _obfs = new()
        {
            //modify original protocol, wrap protocol, obfs param
            { "http_simple", new[] { 0, 1, 1 } },
            { "http_post", new[] { 0, 1, 1 } },
            { "random_head", new[] { 0, 1, 0 } }
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
        private List<byte[]> data_buffer = new();
        private Random random = new();

        public static List<string> SupportedObfs()
        {
            return new(_obfs.Keys);
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
            else if (Method is "http_simple" or "http_post")
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
                    {
                        break;
                    }
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
}
