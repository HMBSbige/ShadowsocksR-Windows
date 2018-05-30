using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Shadowsocks.Controller;
using Shadowsocks.Encryption;

namespace Shadowsocks.Obfs
{
    class AuthAkarin : VerifySimpleBase
    {
        protected class AuthDataAesChain : AuthData
        {
        }

        public AuthAkarin(string method)
            : base(method)
        {
            has_sent_header = false;
            has_recv_header = false;
            pack_id = 1;
            recv_id = 1;
            SALT = method;
            byte[] bytes = new byte[4];
            g_random.GetBytes(bytes);
            random = new Random(BitConverter.ToInt32(bytes, 0));
        }

        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
            {"auth_akarin_rand", new int[]{1, 0, 1}},
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
        protected xorshift128plus random_client = new xorshift128plus(0);
        protected xorshift128plus random_server = new xorshift128plus(0);
        protected IEncryptor encryptor;
        protected int send_tcp_mss = 2000;
        protected int recv_tcp_mss = 2000;
        protected List<int> send_back_cmd = new List<int>();

        protected const int overhead = 4;

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

        protected virtual int GetSendRandLen(int datalength, xorshift128plus random, byte[] last_hash)
        {
            if (datalength + Server.overhead > send_tcp_mss)
            {
                random.init_from_bin(last_hash, datalength);
                return (int)(random.next() % 521);
            }
            if (datalength >= 1440 || datalength + Server.overhead == recv_tcp_mss)
                return 0;
            random.init_from_bin(last_hash, datalength);
            if (datalength > 1300)
                return (int)(random.next() % 31);
            if (datalength > 900)
                return (int)(random.next() % 127);
            if (datalength > 400)
                return (int)(random.next() % 521);
            return (int)(random.next() % (ulong)(send_tcp_mss - datalength - Server.overhead));
            //return (int)(random.next() % 1021);
        }

        protected virtual int GetRecvRandLen(int datalength, xorshift128plus random, byte[] last_hash)
        {
            if (datalength + Server.overhead > recv_tcp_mss)
            {
                random.init_from_bin(last_hash, datalength);
                return (int)(random.next() % 521);
            }
            if (datalength >= 1440 || datalength + Server.overhead == recv_tcp_mss)
                return 0;
            random.init_from_bin(last_hash, datalength);
            if (datalength > 1300)
                return (int)(random.next() % 31);
            if (datalength > 900)
                return (int)(random.next() % 127);
            if (datalength > 400)
                return (int)(random.next() % 521);
            return (int)(random.next() % (ulong)(recv_tcp_mss - datalength - Server.overhead));
            //return (int)(random.next() % 1021);
        }

        protected int UdpGetRandLen(xorshift128plus random, byte[] last_hash)
        {
            random.init_from_bin(last_hash);
            return (int)(random.next() % 127);
        }

        protected int GetSendRandLen(int datalength)
        {
            return GetSendRandLen(datalength, random_client, last_client_hash);
        }

        public void PackData(byte[] data, int datalength, byte[] outdata, out int outlength)
        {
            int cmdlen = 0;
            int rand_len;
            int start_pos = 2;
            if (send_back_cmd.Count > 0)
            {
                cmdlen += 2;
                //TODO
                send_tcp_mss = recv_tcp_mss;
                rand_len = GetSendRandLen(datalength + cmdlen);
                outlength = rand_len + datalength + cmdlen + 2;
                start_pos += cmdlen;
                outdata[0] = (byte)(send_back_cmd[0] ^ last_client_hash[14]);
                outdata[1] = (byte)((send_back_cmd[0] >> 8) ^ last_client_hash[15]);
                outdata[2] = (byte)(datalength ^ last_client_hash[12]);
                outdata[3] = (byte)((datalength >> 8) ^ last_client_hash[13]);
                send_back_cmd.Clear();
            }
            else
            {
                rand_len = GetSendRandLen(datalength);
                outlength = rand_len + datalength + 2;
                outdata[0] = (byte)(datalength ^ last_client_hash[14]);
                outdata[1] = (byte)((datalength >> 8) ^ last_client_hash[15]);
            }
            {
                byte[] rnd_data = new byte[rand_len];
                random.NextBytes(rnd_data);
                encryptor.Encrypt(data, datalength, data, out datalength);
                if (datalength > 0)
                {
                    if (rand_len > 0)
                    {
                        Array.Copy(data, 0, outdata, start_pos, datalength);
                        Array.Copy(rnd_data, 0, outdata, start_pos + datalength, rand_len);
                    }
                    else
                    {
                        Array.Copy(data, 0, outdata, start_pos, datalength);
                    }
                }
                else
                {
                    rnd_data.CopyTo(outdata, start_pos);
                }
            }

            byte[] key = new byte[user_key.Length + 4];
            user_key.CopyTo(key, 0);
            BitConverter.GetBytes(pack_id).CopyTo(key, key.Length - 4);

            MbedTLS.HMAC md5 = CreateHMAC(key);
            ++pack_id;
            {
                byte[] md5data = md5.ComputeHash(outdata, 0, outlength);
                last_client_hash = md5data;
                Array.Copy(md5data, 0, outdata, outlength, 2);
                outlength += 2;
            }
        }

        public void PackAuthData(byte[] data, int datalength, byte[] outdata, out int outlength)
        {
            const int authhead_len = 4 + 8 + 4 + 16 + 4;
            byte[] encrypt = new byte[24];
            AuthDataAesChain authData = this.Server.data as AuthDataAesChain;

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
                    authData.connectionID = (UInt32)BitConverter.ToInt32(authData.clientID, 0) % 0xFFFFFD;
                }
                authData.connectionID += 1;
                Array.Copy(authData.clientID, 0, encrypt, 4, 4);
                Array.Copy(BitConverter.GetBytes(authData.connectionID), 0, encrypt, 8, 4);
            }

            outlength = authhead_len;
            byte[] encrypt_data = new byte[32];
            byte[] key = new byte[Server.iv.Length + Server.key.Length];
            Server.iv.CopyTo(key, 0);
            Server.key.CopyTo(key, Server.iv.Length);

            UInt64 utc_time_second = (UInt64)Math.Floor(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);
            UInt32 utc_time = (UInt32)(utc_time_second);
            Array.Copy(BitConverter.GetBytes(utc_time), 0, encrypt, 0, 4);

            encrypt[12] = (byte)(Server.overhead);
            encrypt[13] = (byte)(Server.overhead >> 8);
            send_tcp_mss = 1024; //random.Next(1024) + 400;
            recv_tcp_mss = send_tcp_mss;
            encrypt[14] = (byte)(send_tcp_mss);
            encrypt[15] = (byte)(send_tcp_mss >> 8);

            // first 12 bytes
            {
                byte[] rnd = new byte[4];
                random.NextBytes(rnd);
                rnd.CopyTo(outdata, 0);
                MbedTLS.HMAC md5 = CreateHMAC(key);
                byte[] md5data = md5.ComputeHash(rnd, 0, rnd.Length);
                last_client_hash = md5data;
                Array.Copy(md5data, 0, outdata, rnd.Length, 8);
            }
            // uid & 16 bytes auth data
            {
                byte[] uid = new byte[4];
                int index_of_split = Server.param.IndexOf(':');
                if (index_of_split > 0)
                {
                    try
                    {
                        uint user = uint.Parse(Server.param.Substring(0, index_of_split));
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
                for (int i = 0; i < 4; ++i)
                {
                    uid[i] ^= last_client_hash[8 + i];
                }

                byte[] encrypt_key = user_key;

                Encryption.IEncryptor encryptor = Encryption.EncryptorFactory.GetEncryptor("aes-128-cbc", System.Convert.ToBase64String(encrypt_key) + SALT, false);
                int enc_outlen;

                encryptor.SetIV(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                encryptor.Encrypt(encrypt, 16, encrypt_data, out enc_outlen);
                encryptor.Dispose();
                Array.Copy(encrypt_data, 0, encrypt, 4, 16);
                uid.CopyTo(encrypt, 0);
            }
            // final HMAC
            {
                MbedTLS.HMAC md5 = CreateHMAC(user_key);
                byte[] md5data = md5.ComputeHash(encrypt, 0, 20);
                last_server_hash = md5data;
                Array.Copy(md5data, 0, encrypt, 20, 4);
            }
            encrypt.CopyTo(outdata, 12);
            encryptor = EncryptorFactory.GetEncryptor("chacha20", System.Convert.ToBase64String(user_key) + System.Convert.ToBase64String(last_client_hash, 0, 16), false);
            {
                byte[] iv = new byte[8];
                Array.Copy(last_client_hash, iv, 8);
                encryptor.SetIV(iv);
            }
            {
                int pack_outlength;
                encryptor.Decrypt(last_server_hash, 8, outdata, out pack_outlength);
            }

            // combine first chunk
            {
                byte[] pack_outdata = new byte[outdata.Length];
                int pack_outlength;
                PackData(data, datalength, pack_outdata, out pack_outlength);
                Array.Copy(pack_outdata, 0, outdata, outlength, pack_outlength);
                outlength += pack_outlength;
            }
        }

        // plaindata == null    try send buffer data, return null if empty buffer
        // datalength == 0      sendback, return 0
        // datalength == -1     keepalive
        public override byte[] ClientPreEncrypt(byte[] plaindata, int datalength, out int outlength)
        {
            byte[] outdata = new byte[datalength + datalength / 10 + 32];
            byte[] packdata = new byte[9000];
            byte[] data = plaindata == null ? send_buffer : plaindata;
            outlength = 0;
            if (data == null)
            {
                return null;
            }
            else if (data == send_buffer)
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
                else
                {
                    Array.Resize(ref send_buffer, send_buffer.Length + datalength);
                    Array.Copy(data, 0, send_buffer, send_buffer.Length - datalength, datalength);
                    data = send_buffer;
                    datalength = send_buffer.Length;
                    send_buffer = null;
                }
            }
            int unit_len = Server.tcp_mss - Server.overhead;
            int ogn_datalength = datalength;
            if (!has_sent_header)
            {
                int _datalength = Math.Min(1200, datalength);
                int outlen;
                PackAuthData(data, _datalength, packdata, out outlen);
                has_sent_header = true;
                Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                Array.Copy(packdata, 0, outdata, outlength, outlen);
                outlength += outlen;
                datalength -= _datalength;
                byte[] newdata = new byte[datalength];
                Array.Copy(data, _datalength, newdata, 0, newdata.Length);
                data = newdata;

                send_buffer = data.Length > 0 ? data : null;

                return outdata;
            }

            if (datalength > 120 * 4 && pack_id < 32)
            {
                int send_len = LinearRandomInt(120 * 16);
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
                int outlen;
                PackData(data, unit_len, packdata, out outlen);
                Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                Array.Copy(packdata, 0, outdata, outlength, outlen);
                outlength += outlen;
                datalength -= unit_len;
                byte[] newdata = new byte[datalength];
                Array.Copy(data, unit_len, newdata, 0, newdata.Length);
                data = newdata;
            }
            if (datalength > 0 || ogn_datalength == -1)
            {
                int outlen;
                if (ogn_datalength == -1)
                    datalength = 0;
                PackData(data, datalength, packdata, out outlen);
                Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                Array.Copy(packdata, 0, outdata, outlength, outlen);
                outlength += outlen;
            }
            last_datalength = ogn_datalength;
            return outdata;
        }

        public override byte[] ClientPostDecrypt(byte[] plaindata, int datalength, out int outlength)
        {
            byte[] outdata = new byte[recv_buf_len + datalength];
            Array.Copy(plaindata, 0, recv_buf, recv_buf_len, datalength);
            recv_buf_len += datalength;
            outlength = 0;
            byte[] key = new byte[user_key.Length + 4];
            user_key.CopyTo(key, 0);
            while (recv_buf_len > 4)
            {
                BitConverter.GetBytes(recv_id).CopyTo(key, key.Length - 4);
                MbedTLS.HMAC md5 = CreateHMAC(key);

                int data_len = ((recv_buf[1] ^ last_server_hash[15]) << 8) + (recv_buf[0] ^ last_server_hash[14]);
                int rand_len = GetRecvRandLen(data_len, random_server, last_server_hash);
                int len = rand_len + data_len;
                if (len >= 4096)
                {
                    throw new ObfsException("ClientPostDecrypt data error");
                }
                if (len + 4 > recv_buf_len)
                    break;

                byte[] md5data = md5.ComputeHash(recv_buf, 0, len + 2);
                if (md5data[0] != recv_buf[len + 2]
                    || md5data[1] != recv_buf[len + 3]
                    )
                {
                    throw new ObfsException("ClientPostDecrypt data uncorrect checksum");
                }

                {
                    int pos = 2;
                    int outlen = data_len;
                    Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                    byte[] data = new byte[outlen];
                    Array.Copy(recv_buf, pos, data, 0, outlen);
                    encryptor.Decrypt(data, outlen, data, out outlen);
                    last_server_hash = md5data;
                    if (recv_id == 1)
                    {
                        Server.tcp_mss = recv_tcp_mss = data[0] | (data[1] << 8);
                        pos = 2;
                        outlen -= 2;
                        send_back_cmd.Add(0xff00);
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
            byte[] outdata = new byte[datalength + 1024];
            if (user_key == null)
            {
                user_id = new byte[4];
                int index_of_split = Server.param.IndexOf(':');
                if (index_of_split > 0)
                {
                    try
                    {
                        uint user = uint.Parse(Server.param.Substring(0, index_of_split));
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
            byte[] auth_data = new byte[3];
            random.NextBytes(auth_data);

            MbedTLS.HMAC md5 = CreateHMAC(Server.key);
            byte[] md5data = md5.ComputeHash(auth_data, 0, auth_data.Length);
            int rand_len = UdpGetRandLen(random_client, md5data);
            byte[] rand_data = new byte[rand_len];
            random.NextBytes(rand_data);
            outlength = datalength + rand_len + 8;
            encryptor = EncryptorFactory.GetEncryptor("chacha20", System.Convert.ToBase64String(user_key) + System.Convert.ToBase64String(md5data, 0, 16), false);
            {
                byte[] iv = new byte[8];
                Array.Copy(Server.key, iv, 8);
                encryptor.SetIV(iv);
            }
            encryptor.Encrypt(plaindata, datalength, outdata, out datalength);
            rand_data.CopyTo(outdata, datalength);
            auth_data.CopyTo(outdata, outlength - 8);
            byte[] uid = new byte[4];
            for (int i = 0; i < 4; ++i)
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
            MbedTLS.HMAC md5 = CreateHMAC(user_key);
            byte[] md5data = md5.ComputeHash(plaindata, 0, datalength - 1);
            if (md5data[0] != plaindata[datalength - 1])
            {
                outlength = 0;
                return plaindata;
            }
            md5 = CreateHMAC(Server.key);
            md5data = md5.ComputeHash(plaindata, datalength - 8, 7);
            int rand_len = UdpGetRandLen(random_server, md5data);
            outlength = datalength - rand_len - 8;
            encryptor = EncryptorFactory.GetEncryptor("chacha20", System.Convert.ToBase64String(user_key) + System.Convert.ToBase64String(md5data, 0, 16), false);
            {
                int temp;
                byte[] iv = new byte[8];
                Array.Copy(Server.key, iv, 8);
                encryptor.Decrypt(iv, 8, plaindata, out temp);
            }
            encryptor.Decrypt(plaindata, outlength, plaindata, out outlength);
            return plaindata;
        }
    }

    class AuthAkarin_spec_a : AuthAkarin
    {
        public AuthAkarin_spec_a(string method)
            : base(method)
        {

        }

        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
            {"auth_akarin_spec_a", new int[]{1, 0, 1}},
        };

        protected int[] data_size_list = null;
        protected int[] data_size_list2 = null;

        public static new List<string> SupportedObfs()
        {
            return new List<string>(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        protected void InitDataSizeList()
        {
            xorshift128plus random = new xorshift128plus(0);
            random.init_from_bin(Server.key);
            int len = (int)(random.next() % 8 + 4);
            List<int> data_list = new List<int>();
            for (int i = 0; i < len; ++i)
            {
                data_list.Add((int)(random.next() % 2340 % 2040 % 1440));
            }
            data_list.Sort();
            data_size_list = data_list.ToArray();

            len = (int)(random.next() % 16 + 8);
            data_list.Clear();
            for (int i = 0; i < len; ++i)
            {
                data_list.Add((int)(random.next() % 2340 % 2040 % 1440));
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
            int low = 0;
            int high = arr.Length - 1;
            int middle = -1;

            if (key > arr[high])
                return arr.Length;

            while (low < high)
            {
                middle = (low + high) / 2;
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

        protected override int GetSendRandLen(int datalength, xorshift128plus random, byte[] last_hash)
        {
            if (datalength + Server.overhead > send_tcp_mss)
            {
                random.init_from_bin(last_hash, datalength);
                return (int)(random.next() % 521);
            }
            if (datalength >= 1440 || datalength + Server.overhead == recv_tcp_mss)
                return 0;
            random.init_from_bin(last_hash, datalength);

            int pos = FindPos(data_size_list, datalength + Server.overhead);
            int final_pos = pos + (int)(random.next() % (ulong)(data_size_list.Length));
            if (final_pos < data_size_list.Length)
            {
                return data_size_list[final_pos] - datalength - Server.overhead;
            }

            pos = FindPos(data_size_list2, datalength + Server.overhead);
            final_pos = pos + (int)(random.next() % (ulong)(data_size_list2.Length));
            if (final_pos < data_size_list2.Length)
            {
                return data_size_list2[final_pos] - datalength - Server.overhead;
            }
            if (final_pos < pos + data_size_list2.Length - 1)
            {
                return 0;
            }
            if (datalength > 1300)
                return (int)(random.next() % 31);
            if (datalength > 900)
                return (int)(random.next() % 127);
            if (datalength > 400)
                return (int)(random.next() % 521);
            return (int)(random.next() % 1021);
        }

        protected override int GetRecvRandLen(int datalength, xorshift128plus random, byte[] last_hash)
        {
            if (datalength + Server.overhead > recv_tcp_mss)
            {
                random.init_from_bin(last_hash, datalength);
                return (int)(random.next() % 521);
            }
            if (datalength >= 1440 || datalength + Server.overhead == recv_tcp_mss)
                return 0;
            random.init_from_bin(last_hash, datalength);

            int pos = FindPos(data_size_list, datalength + Server.overhead);
            int final_pos = pos + (int)(random.next() % (ulong)(data_size_list.Length));
            if (final_pos < data_size_list.Length)
            {
                return data_size_list[final_pos] - datalength - Server.overhead;
            }

            pos = FindPos(data_size_list2, datalength + Server.overhead);
            final_pos = pos + (int)(random.next() % (ulong)(data_size_list2.Length));
            if (final_pos < data_size_list2.Length)
            {
                return data_size_list2[final_pos] - datalength - Server.overhead;
            }
            if (final_pos < pos + data_size_list2.Length - 1)
            {
                return 0;
            }
            if (datalength > 1300)
                return (int)(random.next() % 31);
            if (datalength > 900)
                return (int)(random.next() % 127);
            if (datalength > 400)
                return (int)(random.next() % 521);
            return (int)(random.next() % 1021);
        }

    }
}
