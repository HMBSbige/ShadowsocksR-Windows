﻿using Shadowsocks.Controller;
using Shadowsocks.Encryption;
using Shadowsocks.Encryption.Stream;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Shadowsocks.Obfs
{
    class xorshift128plus
    {
        protected ulong v0, v1;
        protected int init_loop;

        public xorshift128plus(int init_loop_ = 4)
        {
            v0 = v1 = 0;
            init_loop = init_loop_;
        }

        public ulong next()
        {
            var x = v0;
            var y = v1;
            v0 = y;
            x ^= x << 23;
            x ^= y ^ (x >> 17) ^ (y >> 26);
            v1 = x;
            return x + y;
        }

        public void init_from_bin(byte[] bytes)
        {
            var fill_bytes = new byte[16];
            Array.Copy(bytes, fill_bytes, 16);
            v0 = BitConverter.ToUInt64(fill_bytes, 0);
            v1 = BitConverter.ToUInt64(fill_bytes, 8);
        }

        public void init_from_bin(byte[] bytes, int datalength)
        {
            var fill_bytes = new byte[16];
            Array.Copy(bytes, fill_bytes, 16);
            BitConverter.GetBytes((ushort)datalength).CopyTo(fill_bytes, 0);
            v0 = BitConverter.ToUInt64(fill_bytes, 0);
            v1 = BitConverter.ToUInt64(fill_bytes, 8);
            for (var i = 0; i < init_loop; ++i)
            {
                next();
            }
        }
    }

    class AuthChain_a : VerifySimpleBase
    {
        protected class AuthDataAesChain : AuthData
        {
        }

        public AuthChain_a(string method)
            : base(method)
        {
            has_sent_header = false;
            has_recv_header = false;
            pack_id = 1;
            recv_id = 1;
            SALT = method;
            var bytes = new byte[4];
            g_random.GetBytes(bytes);
            random = new Random(BitConverter.ToInt32(bytes, 0));
        }

        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
            {"auth_chain_a", new[]{1, 0, 1}}
        };

        protected bool has_sent_header;
        protected bool has_recv_header;
        protected static RNGCryptoServiceProvider g_random = new RNGCryptoServiceProvider();
        protected string SALT;

        protected uint pack_id;
        protected uint recv_id;
        protected byte[] user_key;
        protected byte[] user_id;
        protected byte[] send_buffer;
        protected int last_datalength;
        protected byte[] last_client_hash;
        protected byte[] last_server_hash;
        protected xorshift128plus random_client = new xorshift128plus();
        protected xorshift128plus random_server = new xorshift128plus();
        protected IEncryptor encryptor;

        protected const int overhead = 4;

        public static List<string> SupportedObfs()
        {
            return new List<string>(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        protected override void Disposing()
        {
            if (encryptor != null)
            {
                encryptor.Dispose();
                encryptor = null;
            }
        }

        public override object InitData()
        {
            return new AuthDataAesChain();
        }

        public override bool isKeepAlive()
        {
            return true;
        }

        public override bool isAlwaysSendback()
        {
            return true;
        }

        public override int GetOverhead()
        {
            return overhead;
        }

        protected MbedTLS.HMAC CreateHMAC(byte[] key)
        {
            return new MbedTLS.HMAC_MD5(key);
        }

        protected virtual int GetRandLen(int datalength, xorshift128plus rd, byte[] last_hash)
        {
            if (datalength > 1440)
                return 0;
            rd.init_from_bin(last_hash, datalength);
            if (datalength > 1300)
                return (int)(rd.next() % 31);
            if (datalength > 900)
                return (int)(rd.next() % 127);
            if (datalength > 400)
                return (int)(rd.next() % 521);
            return (int)(rd.next() % 1021);
        }

        protected int UdpGetRandLen(xorshift128plus rd, byte[] last_hash)
        {
            rd.init_from_bin(last_hash);
            return (int)(rd.next() % 127);
        }

        protected int GetRandStartPos(int rand_len, xorshift128plus rd)
        {
            if (rand_len > 0)
                return (int)(rd.next() % 8589934609 % (ulong)rand_len);
            return 0;
        }

        protected int GetRandLen(int datalength)
        {
            return GetRandLen(datalength, random_client, last_client_hash);
        }

        public void PackData(byte[] data, int datalength, byte[] outdata, out int outlength)
        {
            var rand_len = GetRandLen(datalength);
            outlength = rand_len + datalength + 2;
            outdata[0] = (byte)(datalength ^ last_client_hash[14]);
            outdata[1] = (byte)((datalength >> 8) ^ last_client_hash[15]);
            {
                var rnd_data = new byte[rand_len];
                random.NextBytes(rnd_data);
                encryptor.Encrypt(data, datalength, data, out datalength);
                if (datalength > 0)
                {
                    if (rand_len > 0)
                    {
                        var start_pos = GetRandStartPos(rand_len, random_client);
                        Array.Copy(data, 0, outdata, 2 + start_pos, datalength);
                        Array.Copy(rnd_data, 0, outdata, 2, start_pos);
                        Array.Copy(rnd_data, start_pos, outdata, 2 + start_pos + datalength, rand_len - start_pos);
                    }
                    else
                    {
                        Array.Copy(data, 0, outdata, 2, datalength);
                    }
                }
                else
                {
                    rnd_data.CopyTo(outdata, 2);
                }
            }

            var key = new byte[user_key.Length + 4];
            user_key.CopyTo(key, 0);
            BitConverter.GetBytes(pack_id).CopyTo(key, key.Length - 4);

            var md5 = CreateHMAC(key);
            ++pack_id;
            {
                var md5data = md5.ComputeHash(outdata, 0, outlength);
                last_client_hash = md5data;
                Array.Copy(md5data, 0, outdata, outlength, 2);
                outlength += 2;
            }
        }

        public virtual void OnInitAuthData(ulong unixTimestamp)
        {

        }

        public void PackAuthData(byte[] data, int datalength, byte[] outdata, out int outlength)
        {
            const int authhead_len = 4 + 8 + 4 + 16 + 4;
            var encrypt = new byte[24];

            if (Server.data is AuthDataAesChain authData)
            {
                lock (authData)
                {
                    if (authData.connectionID > 0xFF000000)
                    {
                        authData.clientID = null;
                    }

                    if (authData.clientID == null)
                    {
                        authData.clientID = new byte[4];
                        g_random.GetBytes(authData.clientID);
                        authData.connectionID = (uint)BitConverter.ToInt32(authData.clientID, 0) % 0xFFFFFD;
                    }

                    authData.connectionID += 1;
                    Array.Copy(authData.clientID, 0, encrypt, 4, 4);
                    Array.Copy(BitConverter.GetBytes(authData.connectionID), 0, encrypt, 8, 4);
                }
            }

            outlength = authhead_len;
            var encrypt_data = new byte[32];
            var key = new byte[Server.Iv.Length + Server.key.Length];
            Server.Iv.CopyTo(key, 0);
            Server.key.CopyTo(key, Server.Iv.Length);

            var utc_time_second = (ulong)Math.Floor(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);
            var utc_time = (uint)utc_time_second;
            Array.Copy(BitConverter.GetBytes(utc_time), 0, encrypt, 0, 4);
            OnInitAuthData(utc_time_second);

            encrypt[12] = (byte)Server.overhead;
            encrypt[13] = (byte)(Server.overhead >> 8);

            // first 12 bytes
            {
                var rnd = new byte[4];
                random.NextBytes(rnd);
                rnd.CopyTo(outdata, 0);
                var md5 = CreateHMAC(key);
                var md5data = md5.ComputeHash(rnd, 0, rnd.Length);
                last_client_hash = md5data;
                Array.Copy(md5data, 0, outdata, rnd.Length, 8);
            }
            // uid & 16 bytes auth data
            {
                var uid = new byte[4];
                var index_of_split = Server.param.IndexOf(':');
                if (index_of_split > 0)
                {
                    try
                    {
                        var user = uint.Parse(Server.param.Substring(0, index_of_split));
                        user_key = System.Text.Encoding.UTF8.GetBytes(Server.param.Substring(index_of_split + 1));
                        BitConverter.GetBytes(user).CopyTo(uid, 0);
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(LogLevel.Warn, $"Faild to parse auth param, fallback to basic mode. {ex}");
                    }
                }
                if (user_key == null)
                {
                    random.NextBytes(uid);
                    user_key = Server.key;
                }
                for (var i = 0; i < 4; ++i)
                {
                    uid[i] ^= last_client_hash[8 + i];
                }

                var encrypt_key = user_key;

                var streamEncryptor = (StreamEncryptor)EncryptorFactory.GetEncryptor("aes-128-cbc", Convert.ToBase64String(encrypt_key) + SALT);

                streamEncryptor.SetIV(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                streamEncryptor.Encrypt(encrypt, 16, encrypt_data, out _);
                streamEncryptor.Dispose();
                Array.Copy(encrypt_data, 0, encrypt, 4, 16);
                uid.CopyTo(encrypt, 0);
            }
            // final HMAC
            {
                var md5 = CreateHMAC(user_key);
                var md5data = md5.ComputeHash(encrypt, 0, 20);
                last_server_hash = md5data;
                Array.Copy(md5data, 0, encrypt, 20, 4);
            }
            encrypt.CopyTo(outdata, 12);
            encryptor = EncryptorFactory.GetEncryptor("rc4", Convert.ToBase64String(user_key) + Convert.ToBase64String(last_client_hash, 0, 16));

            // combine first chunk
            {
                var pack_outdata = new byte[outdata.Length];
                PackData(data, datalength, pack_outdata, out var pack_outlength);
                Array.Copy(pack_outdata, 0, outdata, outlength, pack_outlength);
                outlength += pack_outlength;
            }
        }

        // plaindata == null    try send buffer data, return null if empty buffer
        // datalength == 0      sendback, return 0
        // datalength == -1     keepalive
        public override byte[] ClientPreEncrypt(byte[] plaindata, int datalength, out int outlength)
        {
            var outdata = new byte[datalength + datalength / 10 + 32];
            var packdata = new byte[9000];
            var data = plaindata == null ? send_buffer : plaindata;
            outlength = 0;
            if (data == null)
            {
                return null;
            }

            if (data == send_buffer)
            {
                datalength = send_buffer.Length;
                send_buffer = null;
            }
            else if (send_buffer != null)
            {
                if (datalength <= 0)
                {
                    return outdata;
                }

                Array.Resize(ref send_buffer, send_buffer.Length + datalength);
                Array.Copy(data, 0, send_buffer, send_buffer.Length - datalength, datalength);
                data = send_buffer;
                datalength = send_buffer.Length;
                send_buffer = null;
            }
            var unit_len = Server.tcp_mss - Server.overhead;
            var ogn_datalength = datalength;
            if (!has_sent_header)
            {
                var _datalength = Math.Min(1200, datalength);
                PackAuthData(data, _datalength, packdata, out var outlen);
                has_sent_header = true;
                Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                Array.Copy(packdata, 0, outdata, outlength, outlen);
                outlength += outlen;
                datalength -= _datalength;
                var newdata = new byte[datalength];
                Array.Copy(data, _datalength, newdata, 0, newdata.Length);
                data = newdata;

                send_buffer = data.Length > 0 ? data : null;

                return outdata;
            }

            if (datalength > 120 * 4 && pack_id < 32)
            {
                var send_len = LinearRandomInt(120 * 16);
                if (send_len < datalength)
                {
                    send_len = TrapezoidRandomInt(Math.Min(datalength - 1, Server.tcp_mss - overhead) - 1, -0.3) + 1;  // must less than datalength

                    send_len = datalength - send_len;

                    if (send_len > 0)
                    {
                        send_buffer = new byte[send_len];
                        Array.Copy(data, datalength - send_len, send_buffer, 0, send_len);
                        datalength -= send_len;
                    }
                }
            }
            while (datalength > unit_len)
            {
                PackData(data, unit_len, packdata, out var outlen);
                Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                Array.Copy(packdata, 0, outdata, outlength, outlen);
                outlength += outlen;
                datalength -= unit_len;
                var newdata = new byte[datalength];
                Array.Copy(data, unit_len, newdata, 0, newdata.Length);
                data = newdata;
            }
            if (datalength > 0 || ogn_datalength == -1)
            {
                if (ogn_datalength == -1)
                    datalength = 0;
                PackData(data, datalength, packdata, out var outlen);
                Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                Array.Copy(packdata, 0, outdata, outlength, outlen);
                outlength += outlen;
            }
            last_datalength = ogn_datalength;
            return outdata;
        }

        public override byte[] ClientPostDecrypt(byte[] plaindata, int datalength, out int outlength)
        {
            var outdata = new byte[recv_buf_len + datalength];
            Array.Copy(plaindata, 0, recv_buf, recv_buf_len, datalength);
            recv_buf_len += datalength;
            outlength = 0;
            var key = new byte[user_key.Length + 4];
            user_key.CopyTo(key, 0);
            while (recv_buf_len > 4)
            {
                BitConverter.GetBytes(recv_id).CopyTo(key, key.Length - 4);
                var md5 = CreateHMAC(key);

                var data_len = ((recv_buf[1] ^ last_server_hash[15]) << 8) + (recv_buf[0] ^ last_server_hash[14]);
                var rand_len = GetRandLen(data_len, random_server, last_server_hash);
                var len = rand_len + data_len;
                if (len >= 4096)
                {
                    throw new ObfsException("ClientPostDecrypt data error");
                }
                if (len + 4 > recv_buf_len)
                    break;

                var md5data = md5.ComputeHash(recv_buf, 0, len + 2);
                if (md5data[0] != recv_buf[len + 2]
                    || md5data[1] != recv_buf[len + 3]
                    )
                {
                    throw new ObfsException("ClientPostDecrypt data uncorrect checksum");
                }

                {
                    int pos;
                    if (data_len > 0 && rand_len > 0)
                    {
                        pos = 2 + GetRandStartPos(rand_len, random_server);
                    }
                    else
                    {
                        pos = 2;
                    }
                    var outlen = data_len;
                    Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                    var data = new byte[outlen];
                    Array.Copy(recv_buf, pos, data, 0, outlen);
                    encryptor.Decrypt(data, outlen, data, out outlen);
                    last_server_hash = md5data;
                    if (recv_id == 1)
                    {
                        Server.tcp_mss = data[0] | (data[1] << 8);
                        pos = 2;
                        outlen -= 2;
                    }
                    else
                    {
                        pos = 0;
                    }
                    Array.Copy(data, pos, outdata, outlength, outlen);
                    outlength += outlen;
                    recv_buf_len -= len + 4;
                    Array.Copy(recv_buf, len + 4, recv_buf, 0, recv_buf_len);
                    ++recv_id;
                }
            }
            return outdata;
        }

        public override byte[] ClientUdpPreEncrypt(byte[] plaindata, int datalength, out int outlength)
        {
            var outdata = new byte[datalength + 1024];
            if (user_key == null)
            {
                user_id = new byte[4];
                var index_of_split = Server.param.IndexOf(':');
                if (index_of_split > 0)
                {
                    try
                    {
                        var user = uint.Parse(Server.param.Substring(0, index_of_split));
                        user_key = System.Text.Encoding.UTF8.GetBytes(Server.param.Substring(index_of_split + 1));
                        BitConverter.GetBytes(user).CopyTo(user_id, 0);
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(LogLevel.Warn, $"Faild to parse auth param, fallback to basic mode. {ex}");
                    }
                }
                if (user_key == null)
                {
                    random.NextBytes(user_id);
                    user_key = Server.key;
                }
            }
            var auth_data = new byte[3];
            random.NextBytes(auth_data);

            var md5 = CreateHMAC(Server.key);
            var md5data = md5.ComputeHash(auth_data, 0, auth_data.Length);
            var rand_len = UdpGetRandLen(random_client, md5data);
            var rand_data = new byte[rand_len];
            random.NextBytes(rand_data);
            outlength = datalength + rand_len + 8;
            encryptor = EncryptorFactory.GetEncryptor("rc4", Convert.ToBase64String(user_key) + Convert.ToBase64String(md5data, 0, 16));
            encryptor.Encrypt(plaindata, datalength, outdata, out datalength);
            rand_data.CopyTo(outdata, datalength);
            auth_data.CopyTo(outdata, outlength - 8);
            var uid = new byte[4];
            for (var i = 0; i < 4; ++i)
            {
                uid[i] = (byte)(user_id[i] ^ md5data[i]);
            }
            uid.CopyTo(outdata, outlength - 5);
            {
                md5 = CreateHMAC(user_key);
                md5data = md5.ComputeHash(outdata, 0, outlength - 1);
                Array.Copy(md5data, 0, outdata, outlength - 1, 1);
            }
            return outdata;
        }

        public override byte[] ClientUdpPostDecrypt(byte[] plaindata, int datalength, out int outlength)
        {
            if (datalength <= 8)
            {
                outlength = 0;
                return plaindata;
            }
            var md5 = CreateHMAC(user_key);
            var md5data = md5.ComputeHash(plaindata, 0, datalength - 1);
            if (md5data[0] != plaindata[datalength - 1])
            {
                outlength = 0;
                return plaindata;
            }
            md5 = CreateHMAC(Server.key);
            md5data = md5.ComputeHash(plaindata, datalength - 8, 7);
            var rand_len = UdpGetRandLen(random_server, md5data);
            outlength = datalength - rand_len - 8;
            encryptor = EncryptorFactory.GetEncryptor("rc4", Convert.ToBase64String(user_key) + Convert.ToBase64String(md5data, 0, 16));
            encryptor.Decrypt(plaindata, outlength, plaindata, out outlength);
            return plaindata;
        }
    }

    class AuthChain_b : AuthChain_a
    {
        public AuthChain_b(string method)
            : base(method)
        {

        }

        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
            {"auth_chain_b", new[]{1, 0, 1}}
        };

        protected int[] data_size_list;
        protected int[] data_size_list2;

        public static new List<string> SupportedObfs()
        {
            return new List<string>(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        protected virtual void InitDataSizeList()
        {
            var rd = new xorshift128plus();
            rd.init_from_bin(Server.key);
            var len = (int)(rd.next() % 8 + 4);
            var data_list = new List<int>();
            for (var i = 0; i < len; ++i)
            {
                data_list.Add((int)(rd.next() % 2340 % 2040 % 1440));
            }
            data_list.Sort();
            data_size_list = data_list.ToArray();

            len = (int)(rd.next() % 16 + 8);
            data_list.Clear();
            for (var i = 0; i < len; ++i)
            {
                data_list.Add((int)(rd.next() % 2340 % 2040 % 1440));
            }
            data_list.Sort();
            data_size_list2 = data_list.ToArray();
        }

        public override void SetServerInfo(ServerInfo serverInfo)
        {
            Server = serverInfo;
            InitDataSizeList();
        }

        protected int FindPos(int[] arr, int key)
        {
            var low = 0;
            var high = arr.Length - 1;

            if (key > arr[high])
                return arr.Length;

            while (low < high)
            {
                var middle = (low + high) / 2;
                if (key > arr[middle])
                {
                    low = middle + 1;
                }
                else if (key <= arr[middle])
                {
                    high = middle;
                }
            }
            return low;
        }

        protected override int GetRandLen(int datalength, xorshift128plus rd, byte[] last_hash)
        {
            if (datalength >= 1440)
                return 0;
            rd.init_from_bin(last_hash, datalength);

            var pos = FindPos(data_size_list, datalength + Server.overhead);
            var final_pos = pos + (int)(rd.next() % (ulong)data_size_list.Length);
            if (final_pos < data_size_list.Length)
            {
                return data_size_list[final_pos] - datalength - Server.overhead;
            }

            pos = FindPos(data_size_list2, datalength + Server.overhead);
            final_pos = pos + (int)(rd.next() % (ulong)data_size_list2.Length);
            if (final_pos < data_size_list2.Length)
            {
                return data_size_list2[final_pos] - datalength - Server.overhead;
            }
            if (final_pos < pos + data_size_list2.Length - 1)
            {
                return 0;
            }
            if (datalength > 1300)
                return (int)(rd.next() % 31);
            if (datalength > 900)
                return (int)(rd.next() % 127);
            if (datalength > 400)
                return (int)(rd.next() % 521);
            return (int)(rd.next() % 1021);
        }

    }

    class AuthChain_c : AuthChain_b
    {
        public AuthChain_c(string method)
            : base(method)
        {

        }

        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
            {"auth_chain_c", new[]{1, 0, 1}}
        };

        protected int[] data_size_list0;

        public static new List<string> SupportedObfs()
        {
            return new List<string>(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        protected override void InitDataSizeList()
        {
            var rd = new xorshift128plus();
            rd.init_from_bin(Server.key);
            var len = (int)(rd.next() % (8 + 16) + (4 + 8));
            var data_list = new List<int>();
            for (var i = 0; i < len; ++i)
            {
                data_list.Add((int)(rd.next() % 2340 % 2040 % 1440));
            }
            data_list.Sort();
            data_size_list0 = data_list.ToArray();
        }

        public override void SetServerInfo(ServerInfo serverInfo)
        {
            Server = serverInfo;
            InitDataSizeList();
        }

        protected override int GetRandLen(int datalength, xorshift128plus rd, byte[] last_hash)
        {
            var other_data_size = datalength + Server.overhead;

            // 一定要在random使用前初始化，以保证服务器与客户端同步，保证包大小验证结果正确
            rd.init_from_bin(last_hash, datalength);
            if (other_data_size >= data_size_list0[data_size_list0.Length - 1])
            {
                if (datalength >= 1440)
                    return 0;
                if (datalength > 1300)
                    return (int)(rd.next() % 31);
                if (datalength > 900)
                    return (int)(rd.next() % 127);
                if (datalength > 400)
                    return (int)(rd.next() % 521);
                return (int)(rd.next() % 1021);
            }

            var pos = FindPos(data_size_list0, other_data_size);
            var final_pos = pos + (int)(rd.next() % (ulong)(data_size_list0.Length - pos));
            return data_size_list0[final_pos] - other_data_size;
        }

    }

    class AuthChain_d : AuthChain_c
    {
        public AuthChain_d(string method)
            : base(method)
        {

        }

        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
            {"auth_chain_d", new[]{1, 0, 1}}
        };

        public static new List<string> SupportedObfs()
        {
            return new List<string>(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        protected void CheckAndPatchDataSize(List<int> data_list, xorshift128plus rd)
        {
            if (data_list[data_list.Count - 1] < 1300 && data_list.Count < 64)
            {
                data_list.Add((int)(rd.next() % 2340 % 2040 % 1440));
                CheckAndPatchDataSize(data_list, rd);
            }
        }

        protected override void InitDataSizeList()
        {
            var rd = new xorshift128plus();
            rd.init_from_bin(Server.key);
            var len = (int)(rd.next() % (8 + 16) + (4 + 8));
            var data_list = new List<int>();
            for (var i = 0; i < len; ++i)
            {
                data_list.Add((int)(rd.next() % 2340 % 2040 % 1440));
            }
            data_list.Sort();
            var old_len = data_list.Count;
            CheckAndPatchDataSize(data_list, rd);
            if (old_len != data_list.Count)
            {
                data_list.Sort();
            }
            data_size_list0 = data_list.ToArray();
        }

        public override void SetServerInfo(ServerInfo serverInfo)
        {
            Server = serverInfo;
            InitDataSizeList();
        }

        protected override int GetRandLen(int datalength, xorshift128plus rd, byte[] last_hash)
        {
            var other_data_size = datalength + Server.overhead;

            if (other_data_size >= data_size_list0[data_size_list0.Length - 1])
            {
                return 0;
            }

            rd.init_from_bin(last_hash, datalength);
            var pos = FindPos(data_size_list0, other_data_size);
            var final_pos = pos + (int)(rd.next() % (ulong)(data_size_list0.Length - pos));
            return data_size_list0[final_pos] - other_data_size;
        }

    }

    class AuthChain_e : AuthChain_d
    {
        public AuthChain_e(string method)
            : base(method)
        {

        }

        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
            {"auth_chain_e", new[]{1, 0, 1}}
        };

        public static new List<string> SupportedObfs()
        {
            return new List<string>(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        protected override int GetRandLen(int datalength, xorshift128plus rd, byte[] last_hash)
        {
            rd.init_from_bin(last_hash, datalength);
            var other_data_size = datalength + Server.overhead;

            if (other_data_size >= data_size_list0[data_size_list0.Length - 1])
            {
                return 0;
            }

            var pos = FindPos(data_size_list0, other_data_size);
            return data_size_list0[pos] - other_data_size;
        }

    }

    class AuthChain_f : AuthChain_e
    {
        public AuthChain_f(string method)
            : base(method)
        {

        }

        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
            {"auth_chain_f", new[]{1, 0, 1}}
        };

        public static new List<string> SupportedObfs()
        {
            return new List<string>(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        protected ulong key_change_interval = 60 * 60 * 24;    // a day by second
        protected ulong key_change_datetime_key;
        protected List<byte> key_change_datetime_key_bytes = new List<byte>();

        protected override void InitDataSizeList()
        {
            var rd = new xorshift128plus();
            var newKey = new byte[Server.key.Length];
            Server.key.CopyTo(newKey, 0);
            for (var i = 0; i < 8; i++)
            {
                newKey[i] ^= key_change_datetime_key_bytes[i];
            }
            rd.init_from_bin(newKey);
            var len = (int)(rd.next() % (8 + 16) + (4 + 8));
            var data_list = new List<int>();
            for (var i = 0; i < len; ++i)
            {
                data_list.Add((int)(rd.next() % 2340 % 2040 % 1440));
            }
            data_list.Sort();
            var old_len = data_list.Count;
            CheckAndPatchDataSize(data_list, rd);
            if (old_len != data_list.Count)
            {
                data_list.Sort();
            }
            data_size_list0 = data_list.ToArray();
        }

        public override void SetServerInfo(ServerInfo serverInfo)
        {
            Server = serverInfo;
            var protocalParams = serverInfo.param;
            if (protocalParams != "")
            {
                if (-1 != protocalParams.IndexOf("#", StringComparison.Ordinal))
                {
                    protocalParams = protocalParams.Split('#')[1];
                }

                if (ulong.TryParse(protocalParams, out var interval))
                {
                    key_change_interval = interval;
                }
            }
        }

        public override void OnInitAuthData(ulong unixTimestamp)
        {
            key_change_datetime_key = unixTimestamp / key_change_interval;
            for (var i = 7; i > -1; --i)
            {
                var b = (byte)(key_change_datetime_key >> (8 * i) & 0xFF);
                key_change_datetime_key_bytes.Add(b);
            }
            InitDataSizeList();
        }

    }
}
