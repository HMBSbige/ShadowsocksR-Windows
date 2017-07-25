using System;
using System.Collections.Generic;
using Shadowsocks.Controller;
using System.Security.Cryptography;
using Shadowsocks.Encryption;

namespace Shadowsocks.Obfs
{
    public class AuthData : VerifyData
    {
        public byte[] clientID;
        public UInt32 connectionID;
    }

    public class AuthSHA1 : VerifySimpleBase
    {
        public AuthSHA1(string method)
            : base(method)
        {
            has_sent_header = false;
            has_recv_header = false;
        }
        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
            {"auth_sha1", new int[]{1, 0, 1}},
        };

        protected bool has_sent_header;
        protected bool has_recv_header;
        protected static RNGCryptoServiceProvider g_random = new RNGCryptoServiceProvider();

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
            return new AuthData();
        }

        public void PackData(byte[] data, int datalength, byte[] outdata, out int outlength)
        {
            int rand_len = datalength > 1300 ? 1 : LinearRandomInt(64) + 1;
            outlength = rand_len + datalength + 6;
            if (datalength > 0)
                Array.Copy(data, 0, outdata, rand_len + 2, datalength);
            outdata[0] = (byte)(outlength >> 8);
            outdata[1] = (byte)(outlength);
            outdata[2] = (byte)(rand_len);
            ulong adler = Util.Adler32.CalcAdler32(outdata, outlength - 4);
            BitConverter.GetBytes((uint)adler).CopyTo(outdata, outlength - 4);
        }

        public void PackAuthData(byte[] data, int datalength, byte[] outdata, out int outlength)
        {
            int rand_len = LinearRandomInt(250) + 1;
            int data_offset = rand_len + 4 + 2;
            outlength = data_offset + datalength + 12 + 10;
            AuthData authData = (AuthData)this.Server.data;
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
                    authData.connectionID = (UInt32)LinearRandomInt(0x1000000);
                }
                authData.connectionID += 1;
                Array.Copy(authData.clientID, 0, outdata, data_offset + 4, 4);
                Array.Copy(BitConverter.GetBytes(authData.connectionID), 0, outdata, data_offset + 8, 4);
            }
            UInt64 utc_time_second = (UInt64)Math.Floor(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);
            UInt32 utc_time = (UInt32)(utc_time_second);
            Array.Copy(BitConverter.GetBytes(utc_time), 0, outdata, data_offset, 4);

            Array.Copy(data, 0, outdata, data_offset + 12, datalength);
            outdata[4] = (byte)(outlength >> 8);
            outdata[5] = (byte)(outlength);
            outdata[6] = (byte)(rand_len);

            ulong crc32 = Util.CRC32.CalcCRC32(Server.key, (int)Server.key.Length);
            BitConverter.GetBytes((uint)crc32).CopyTo(outdata, 0);

            byte[] key = new byte[Server.iv.Length + Server.key.Length];
            Server.iv.CopyTo(key, 0);
            Server.key.CopyTo(key, Server.iv.Length);

            HMACSHA1 sha1 = new HMACSHA1(key);
            byte[] sha1data = sha1.ComputeHash(outdata, 0, outlength - 10);

            Array.Copy(sha1data, 0, outdata, outlength - 10, 10);
        }

        public override byte[] ClientPreEncrypt(byte[] plaindata, int datalength, out int outlength)
        {
            if (plaindata == null)
            {
                outlength = 0;
                return plaindata;
            }
            byte[] outdata = new byte[datalength + datalength / 10 + 32];
            byte[] packdata = new byte[9000];
            byte[] data = plaindata;
            outlength = 0;
            const int unit_len = 8100;
            if (!has_sent_header)
            {
                int headsize = GetHeadSize(plaindata, 30);
                int _datalength = Math.Min(LinearRandomInt(32) + headsize, datalength);
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
            if (datalength > 0)
            {
                int outlen;
                PackData(data, datalength, packdata, out outlen);
                Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                Array.Copy(packdata, 0, outdata, outlength, outlen);
                outlength += outlen;
            }
            return outdata;
        }

        public override byte[] ClientPostDecrypt(byte[] plaindata, int datalength, out int outlength)
        {
            byte[] outdata = new byte[recv_buf_len + datalength];
            Array.Copy(plaindata, 0, recv_buf, recv_buf_len, datalength);
            recv_buf_len += datalength;
            outlength = 0;
            while (recv_buf_len > 2)
            {
                int len = (recv_buf[0] << 8) + recv_buf[1];
                if (len >= 8192 || len < 7)
                {
                    throw new ObfsException("ClientPostDecrypt data error");
                }
                if (len > recv_buf_len)
                    break;

                if (Util.Adler32.CheckAdler32(recv_buf, len))
                {
                    int pos = recv_buf[2] + 2;
                    int outlen = len - pos - 4;
                    Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                    Array.Copy(recv_buf, pos, outdata, outlength, outlen);
                    outlength += outlen;
                    recv_buf_len -= len;
                    Array.Copy(recv_buf, len, recv_buf, 0, recv_buf_len);
                }
                else
                {
                    throw new ObfsException("ClientPostDecrypt data uncorrect checksum");
                }
            }
            return outdata;
        }
    }

    public class AuthSHA1V2 : VerifySimpleBase
    {
        public AuthSHA1V2(string method)
            : base(method)
        {
            has_sent_header = false;
            has_recv_header = false;
        }
        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
            {"auth_sha1_v2", new int[]{1, 0, 1}},
        };

        protected bool has_sent_header;
        protected bool has_recv_header;
        protected static RNGCryptoServiceProvider g_random = new RNGCryptoServiceProvider();

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
            return new AuthData();
        }

        public override bool isKeepAlive()
        {
            return true;
        }

        public override bool isAlwaysSendback()
        {
            return true;
        }

        public void PackData(byte[] data, int datalength, byte[] outdata, out int outlength)
        {
            int rand_len = (datalength >= 1300 ? 0 : (datalength > 400 ? LinearRandomInt(128) : LinearRandomInt(1024))) + 1;
            outlength = rand_len + datalength + 6;
            if (datalength > 0)
                Array.Copy(data, 0, outdata, rand_len + 2, datalength);
            outdata[0] = (byte)(outlength >> 8);
            outdata[1] = (byte)(outlength);
            {
                byte[] rnd_data = new byte[rand_len];
                random.NextBytes(rnd_data);
                rnd_data.CopyTo(outdata, 2);
            }
            if (rand_len < 128)
            {
                outdata[2] = (byte)(rand_len);
            }
            else
            {
                outdata[2] = 0xFF;
                outdata[3] = (byte)(rand_len >> 8);
                outdata[4] = (byte)(rand_len);
            }
            ulong adler = Util.Adler32.CalcAdler32(outdata, outlength - 4);
            BitConverter.GetBytes((uint)adler).CopyTo(outdata, outlength - 4);
        }

        public void PackAuthData(byte[] data, int datalength, byte[] outdata, out int outlength)
        {
            int rand_len = (datalength > 400 ? LinearRandomInt(128) : LinearRandomInt(1024)) + 1;
            int data_offset = rand_len + 4 + 2;
            outlength = data_offset + datalength + 12 + 10;
            AuthData authData = (AuthData)this.Server.data;
            {
                byte[] rnd_data = new byte[rand_len];
                random.NextBytes(rnd_data);
                rnd_data.CopyTo(outdata, data_offset - rand_len);
            }
            lock (authData)
            {
                if (authData.connectionID > 0xFF000000)
                {
                    authData.clientID = null;
                }
                if (authData.clientID == null)
                {
                    authData.clientID = new byte[8];
                    g_random.GetBytes(authData.clientID);
                    authData.connectionID = (UInt32)BitConverter.ToInt64(authData.clientID, 0) % 0xFFFFFD;
                }
                authData.connectionID += 1;
                Array.Copy(authData.clientID, 0, outdata, data_offset, 8);
                Array.Copy(BitConverter.GetBytes(authData.connectionID), 0, outdata, data_offset + 8, 4);
            }

            Array.Copy(data, 0, outdata, data_offset + 12, datalength);
            outdata[4] = (byte)(outlength >> 8);
            outdata[5] = (byte)(outlength);
            if (rand_len < 128)
            {
                outdata[6] = (byte)(rand_len);
            }
            else
            {
                outdata[6] = 0xFF;
                outdata[7] = (byte)(rand_len >> 8);
                outdata[8] = (byte)(rand_len);
            }

            byte[] salt = System.Text.Encoding.UTF8.GetBytes("auth_sha1_v2");
            byte[] crcdata = new byte[salt.Length + Server.key.Length];
            salt.CopyTo(crcdata, 0);
            Server.key.CopyTo(crcdata, salt.Length);
            ulong crc32 = Util.CRC32.CalcCRC32(crcdata, (int)crcdata.Length);
            BitConverter.GetBytes((uint)crc32).CopyTo(outdata, 0);

            byte[] key = new byte[Server.iv.Length + Server.key.Length];
            Server.iv.CopyTo(key, 0);
            Server.key.CopyTo(key, Server.iv.Length);

            HMACSHA1 sha1 = new HMACSHA1(key);
            byte[] sha1data = sha1.ComputeHash(outdata, 0, outlength - 10);

            Array.Copy(sha1data, 0, outdata, outlength - 10, 10);
        }

        public override byte[] ClientPreEncrypt(byte[] plaindata, int datalength, out int outlength)
        {
            if (plaindata == null)
            {
                outlength = 0;
                return plaindata;
            }
            byte[] outdata = new byte[datalength + datalength / 10 + 32];
            byte[] packdata = new byte[9000];
            byte[] data = plaindata;
            outlength = 0;
            const int unit_len = 8100;
            int ogn_datalength = datalength;
            if (!has_sent_header)
            {
                int headsize = GetHeadSize(plaindata, 30);
                int _datalength = Math.Min(LinearRandomInt(32) + headsize, datalength);
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
            }
            while (datalength > unit_len)
            {
                int outlen;
                PackData(data, unit_len, packdata, out outlen);
                Util.Utils.SetArrayMinSize(ref outdata, outlength + outlen);
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
            return outdata;
        }

        public override byte[] ClientPostDecrypt(byte[] plaindata, int datalength, out int outlength)
        {
            byte[] outdata = new byte[recv_buf_len + datalength];
            Array.Copy(plaindata, 0, recv_buf, recv_buf_len, datalength);
            recv_buf_len += datalength;
            outlength = 0;
            while (recv_buf_len > 2)
            {
                int len = (recv_buf[0] << 8) + recv_buf[1];
                if (len >= 8192 || len < 8)
                {
                    throw new ObfsException("ClientPostDecrypt data error");
                }
                if (len > recv_buf_len)
                    break;

                if (Util.Adler32.CheckAdler32(recv_buf, len))
                {
                    int pos = recv_buf[2];
                    if (pos < 255)
                    {
                        pos += 2;
                    }
                    else
                    {
                        pos = ((recv_buf[3] << 8) | recv_buf[4]) + 2;
                    }
                    int outlen = len - pos - 4;
                    Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                    Array.Copy(recv_buf, pos, outdata, outlength, outlen);
                    outlength += outlen;
                    recv_buf_len -= len;
                    Array.Copy(recv_buf, len, recv_buf, 0, recv_buf_len);
                }
                else
                {
                    throw new ObfsException("ClientPostDecrypt data uncorrect checksum");
                }
            }
            return outdata;
        }
    }

    public class AuthSHA1V4 : VerifySimpleBase
    {
        public AuthSHA1V4(string method)
            : base(method)
        {
            has_sent_header = false;
            has_recv_header = false;
        }
        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
            {"auth_sha1_v4", new int[]{1, 0, 1}},
        };

        protected bool has_sent_header;
        protected bool has_recv_header;
        protected static RNGCryptoServiceProvider g_random = new RNGCryptoServiceProvider();
        protected const string SALT = "auth_sha1_v4";
        protected const int overhead = 9;

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
            return new AuthData();
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

        public void PackData(byte[] data, int datalength, byte[] outdata, out int outlength)
        {
            int rand_len = (datalength > 1200 ? 0 : (datalength > 400 ? LinearRandomInt(256) : LinearRandomInt(512))) + 1;
            outlength = rand_len + datalength + 8;
            if (datalength > 0)
                Array.Copy(data, 0, outdata, rand_len + 4, datalength);
            outdata[0] = (byte)(outlength >> 8);
            outdata[1] = (byte)(outlength);
            ulong crc32 = Util.CRC32.CalcCRC32(outdata, 2);
            BitConverter.GetBytes((ushort)crc32).CopyTo(outdata, 2);
            {
                byte[] rnd_data = new byte[rand_len];
                random.NextBytes(rnd_data);
                rnd_data.CopyTo(outdata, 4);
            }
            if (rand_len < 128)
            {
                outdata[4] = (byte)(rand_len);
            }
            else
            {
                outdata[4] = 0xFF;
                outdata[5] = (byte)(rand_len >> 8);
                outdata[6] = (byte)(rand_len);
            }
            ulong adler = Util.Adler32.CalcAdler32(outdata, outlength - 4);
            BitConverter.GetBytes((uint)adler).CopyTo(outdata, outlength - 4);
        }

        public void PackAuthData(byte[] data, int datalength, byte[] outdata, out int outlength)
        {
            int rand_len = (datalength > 400 ? LinearRandomInt(128) : LinearRandomInt(1024)) + 1;
            int data_offset = rand_len + 4 + 2;
            outlength = data_offset + datalength + 12 + 10;
            AuthData authData = (AuthData)this.Server.data;
            {
                byte[] rnd_data = new byte[rand_len];
                random.NextBytes(rnd_data);
                rnd_data.CopyTo(outdata, data_offset - rand_len);
            }
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
                Array.Copy(authData.clientID, 0, outdata, data_offset + 4, 4);
                Array.Copy(BitConverter.GetBytes(authData.connectionID), 0, outdata, data_offset + 8, 4);
            }
            UInt64 utc_time_second = (UInt64)Math.Floor(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);
            UInt32 utc_time = (UInt32)(utc_time_second);
            Array.Copy(BitConverter.GetBytes(utc_time), 0, outdata, data_offset, 4);

            Array.Copy(data, 0, outdata, data_offset + 12, datalength);
            outdata[0] = (byte)(outlength >> 8);
            outdata[1] = (byte)(outlength);
            if (rand_len < 128)
            {
                outdata[6] = (byte)(rand_len);
            }
            else
            {
                outdata[6] = 0xFF;
                outdata[7] = (byte)(rand_len >> 8);
                outdata[8] = (byte)(rand_len);
            }

            byte[] salt = System.Text.Encoding.UTF8.GetBytes(SALT);
            byte[] crcdata = new byte[salt.Length + Server.key.Length + 2];
            salt.CopyTo(crcdata, 2);
            Server.key.CopyTo(crcdata, salt.Length + 2);
            crcdata[0] = outdata[0];
            crcdata[1] = outdata[1];
            ulong crc32 = Util.CRC32.CalcCRC32(crcdata, (int)crcdata.Length);
            BitConverter.GetBytes((uint)crc32).CopyTo(outdata, 2);

            byte[] key = new byte[Server.iv.Length + Server.key.Length];
            Server.iv.CopyTo(key, 0);
            Server.key.CopyTo(key, Server.iv.Length);

            HMACSHA1 sha1 = new HMACSHA1(key);
            byte[] sha1data = sha1.ComputeHash(outdata, 0, outlength - 10);

            Array.Copy(sha1data, 0, outdata, outlength - 10, 10);
        }

        public override byte[] ClientPreEncrypt(byte[] plaindata, int datalength, out int outlength)
        {
            if (plaindata == null)
            {
                outlength = 0;
                return plaindata;
            }
            byte[] outdata = new byte[datalength + datalength / 10 + 32];
            byte[] packdata = new byte[9000];
            byte[] data = plaindata;
            outlength = 0;
            const int unit_len = 8100;
            int ogn_datalength = datalength;
            if (!has_sent_header)
            {
                int headsize = GetHeadSize(plaindata, 30);
                int _datalength = Math.Min(LinearRandomInt(32) + headsize, datalength);
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
            return outdata;
        }

        public override byte[] ClientPostDecrypt(byte[] plaindata, int datalength, out int outlength)
        {
            byte[] outdata = new byte[recv_buf_len + datalength];
            Array.Copy(plaindata, 0, recv_buf, recv_buf_len, datalength);
            recv_buf_len += datalength;
            outlength = 0;
            while (recv_buf_len > 4)
            {
                ulong crc32 = Util.CRC32.CalcCRC32(recv_buf, 2);
                if ((uint)((recv_buf[3] << 8) | recv_buf[2]) != ((uint)crc32 & 0xffff))
                {
                    throw new ObfsException("ClientPostDecrypt data error");
                }
                int len = (recv_buf[0] << 8) + recv_buf[1];
                if (len >= 8192 || len < 8)
                {
                    throw new ObfsException("ClientPostDecrypt data error");
                }
                if (len > recv_buf_len)
                    break;

                if (Util.Adler32.CheckAdler32(recv_buf, len))
                {
                    int pos = recv_buf[4];
                    if (pos < 255)
                    {
                        pos += 4;
                    }
                    else
                    {
                        pos = ((recv_buf[5] << 8) | recv_buf[6]) + 4;
                    }
                    int outlen = len - pos - 4;
                    Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                    Array.Copy(recv_buf, pos, outdata, outlength, outlen);
                    outlength += outlen;
                    recv_buf_len -= len;
                    Array.Copy(recv_buf, len, recv_buf, 0, recv_buf_len);
                }
                else
                {
                    throw new ObfsException("ClientPostDecrypt data uncorrect checksum");
                }
            }
            return outdata;
        }
    }

    public class AuthAES128SHA1 : VerifySimpleBase
    {
        protected delegate byte[] hash_func(byte[] input);

        protected class AuthDataAes128 : AuthData
        {
            public Model.MinSearchTree tree;
        }

        public AuthAES128SHA1(string method)
            : base(method)
        {
            has_sent_header = false;
            has_recv_header = false;
            pack_id = 1;
            recv_id = 1;
            SALT = method;
            if (method == "auth_aes128_md5")
                hash = MbedTLS.MD5;
            else
                hash = MbedTLS.SHA1;
            byte[] bytes = new byte[4];
            g_random.GetBytes(bytes);
            random = new Random(BitConverter.ToInt32(bytes, 0));
        }
        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
            {"auth_aes128_md5", new int[]{1, 0, 1}},
            {"auth_aes128_sha1", new int[]{1, 0, 1}},
        };

        protected bool has_sent_header;
        protected bool has_recv_header;
        protected static RNGCryptoServiceProvider g_random = new RNGCryptoServiceProvider();
        protected string SALT;

        protected uint pack_id;
        protected uint recv_id;
        protected byte[] user_key;
        protected byte[] user_id;
        protected hash_func hash;
        protected byte[] send_buffer;
        protected int last_datalength;

        protected const int overhead = 9; // 2(length) + 2(len-MAC) + 4(data-MAC) + 1(padding)
        //protected int[] packet_cnt;
        protected Dictionary<int, long> packet_cnt = new Dictionary<int, long>();
        //protected int[] packet_mul;
        protected Model.MinSearchTree tree;
        protected const int tree_offset = 9;
        protected DateTime lastSendTime;

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
            return new AuthDataAes128();
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
            if (Method == "auth_aes128_md5")
                return new MbedTLS.HMAC_MD5(key);
            if (Method == "auth_aes128_sha1")
                return new MbedTLS.HMAC_SHA1(key);
            return null;
        }

        protected void Sync()
        {
#if PROTOCOL_STATISTICS
            if (Server.data != null)
            {
                AuthDataAes128 authData = Server.data as AuthDataAes128;
                lock (authData)
                {
                    if (authData.tree != null && packet_cnt != null)
                    {
                        authData.tree.Update(packet_cnt);
                        tree = authData.tree.Clone();
                    }
                }
            }
#endif
        }

        // return final size, include dataLengthMax
        protected int RandomInMin(int dataLengthMin, int dataLengthMax)
        {
            dataLengthMin -= tree_offset;
            dataLengthMax -= tree_offset;
            int len = tree.RandomFindIndex(dataLengthMin - 1, dataLengthMax, random);
            tree.Update(len);
            return len + tree_offset + 1;
        }

        // real packet size. mapping: 1 => 0
        protected void AddPacket(int length)
        {
#if PROTOCOL_STATISTICS
            if (length > tree_offset)
            {
                length -= 1 + tree_offset;
                //if (length >= packet_cnt.Length) length = packet_cnt.Length - 1;
                //packet_cnt[length]++;
                if (length >= tree.Size) length = tree.Size - 1;
                if (packet_cnt.ContainsKey(length))
                {
                    packet_cnt[length] += 1;
                }
                else
                    packet_cnt[length] = 1;
            }
            else
            {
                throw new ObfsException("AddPacket size uncorrect");
            }
#endif
        }

        protected void StatisticsInit(AuthDataAes128 authData)
        {
#if PROTOCOL_STATISTICS
            if (authData.tree == null)
            {
                authData.tree = new Model.MinSearchTree(Server.tcp_mss - tree_offset);
                authData.tree.Init();
            }

            tree = authData.tree.Clone();
#endif
        }

        protected int GetRandLen(int datalength, int fulldatalength, bool nopadding)
        {
            if (nopadding || fulldatalength >= Server.buffer_size)
                return 0;
            int rev_len = Server.tcp_mss - datalength - overhead;
            if (rev_len <= 0)
                return 0;
            if (datalength > 1100)
                return LinearRandomInt(rev_len);
            return TrapezoidRandomInt(rev_len, -0.3);
        }

#if PROTOCOL_STATISTICS
        // packetlength + padding = real_packetlength
        // return size of padding, at least 1
        protected int GenRandLenFull(int packetlength, int fulldatalength, bool nopadding)
        {
            if (nopadding || fulldatalength >= Server.buffer_size)
                return packetlength;
            if (packetlength >= Server.tcp_mss)
            {
                if (packetlength > Server.tcp_mss && packetlength < Server.tcp_mss * 2)
                {
                    return packetlength + TrapezoidRandomInt(Server.tcp_mss * 2 - packetlength, -0.3);
                }
                return packetlength + TrapezoidRandomInt(32, -0.3);
            }
            return RandomInMin(packetlength, Server.tcp_mss - 1);
        }

        protected int GenRandLen(int packetlength, int maxpacketlength)
        {
            return RandomInMin(packetlength, maxpacketlength);
        }
#endif

        public void PackData(byte[] data, int datalength, int fulldatalength, byte[] outdata, out int outlength, bool nopadding = false)
        {
#if !PROTOCOL_STATISTICS
            int rand_len = GetRandLen(datalength, fulldatalength, nopadding) + 1;
#else
            const int overhead = 8;
            int rand_len = GenRandLenFull((datalength == 0 ? 1 : datalength) + overhead + 1, fulldatalength, nopadding)
                - datalength - overhead;
#endif
            outlength = rand_len + datalength + 8;
            if (datalength > 0)
                Array.Copy(data, 0, outdata, rand_len + 4, datalength);
            outdata[0] = (byte)(outlength);
            outdata[1] = (byte)(outlength >> 8);
            byte[] key = new byte[user_key.Length + 4];
            user_key.CopyTo(key, 0);
            BitConverter.GetBytes(pack_id).CopyTo(key, key.Length - 4);
            {
                byte[] rnd_data = new byte[rand_len];
                random.NextBytes(rnd_data);
                rnd_data.CopyTo(outdata, 4);
            }

            MbedTLS.HMAC sha1 = CreateHMAC(key);
            {
                byte[] sha1data = sha1.ComputeHash(outdata, 0, 2);
                Array.Copy(sha1data, 0, outdata, 2, 2);
            }
            if (rand_len < 128)
            {
                outdata[4] = (byte)(rand_len);
            }
            else
            {
                outdata[4] = 0xFF;
                outdata[5] = (byte)(rand_len);
                outdata[6] = (byte)(rand_len >> 8);
            }
            ++pack_id;
            {
                byte[] sha1data = sha1.ComputeHash(outdata, 0, outlength - 4);
                Array.Copy(sha1data, 0, outdata, outlength - 4, 4);
            }
        }

        public void PackAuthData(byte[] data, int datalength, byte[] outdata, out int outlength)
        {
            const int authhead_len = 7 + 4 + 16 + 4;
            const int overhead = authhead_len + 4;
            byte[] encrypt = new byte[24];
            AuthDataAes128 authData = this.Server.data as AuthDataAes128;

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

                StatisticsInit(authData);
            }

#if !PROTOCOL_STATISTICS
            int rand_len = TrapezoidRandomInt(Server.tcp_mss - datalength - overhead + 1, -0.3); //(datalength > 400 ? LinearRandomInt(512) : LinearRandomInt(1024));
#else
            int rand_len = GenRandLenFull(datalength + overhead, datalength, false) - datalength - overhead;
#endif
            int data_offset = rand_len + authhead_len;
            outlength = data_offset + datalength + 4;
            byte[] encrypt_data = new byte[32];
            byte[] key = new byte[Server.iv.Length + Server.key.Length];
            Server.iv.CopyTo(key, 0);
            Server.key.CopyTo(key, Server.iv.Length);

            {
                byte[] rnd_data = new byte[rand_len];
                random.NextBytes(rnd_data);
                rnd_data.CopyTo(outdata, data_offset - rand_len);
            }

            UInt64 utc_time_second = (UInt64)Math.Floor(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);
            UInt32 utc_time = (UInt32)(utc_time_second);
            Array.Copy(BitConverter.GetBytes(utc_time), 0, encrypt, 0, 4);
            encrypt[12] = (byte)(outlength);
            encrypt[13] = (byte)(outlength >> 8);
            encrypt[14] = (byte)(rand_len);
            encrypt[15] = (byte)(rand_len >> 8);

            {
                byte[] uid = new byte[4];
                int index_of_split = Server.param.IndexOf(':');
                if (index_of_split > 0)
                {
                    try
                    {
                        uint user = uint.Parse(Server.param.Substring(0, index_of_split));
                        user_key = hash(System.Text.Encoding.UTF8.GetBytes(Server.param.Substring(index_of_split + 1)));
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

                byte[] encrypt_key = user_key;

                Encryption.IEncryptor encryptor = Encryption.EncryptorFactory.GetEncryptor("aes-128-cbc", System.Convert.ToBase64String(encrypt_key) + SALT);
                int enc_outlen;

                encryptor.SetIV(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                encryptor.Encrypt(encrypt, 16, encrypt_data, out enc_outlen);
                encryptor.Dispose();
                Array.Copy(encrypt_data, 16, encrypt, 4, 16);
                uid.CopyTo(encrypt, 0);
            }
            {
                MbedTLS.HMAC sha1 = CreateHMAC(key);
                byte[] sha1data = sha1.ComputeHash(encrypt, 0, 20);
                Array.Copy(sha1data, 0, encrypt, 20, 4);
            }
            {
                byte[] rnd = new byte[1];
                random.NextBytes(rnd);
                rnd.CopyTo(outdata, 0);
                MbedTLS.HMAC sha1 = CreateHMAC(key);
                byte[] sha1data = sha1.ComputeHash(rnd, 0, rnd.Length);
                Array.Copy(sha1data, 0, outdata, rnd.Length, 7 - rnd.Length);
            }
            encrypt.CopyTo(outdata, 7);
            Array.Copy(data, 0, outdata, data_offset, datalength);

            {
                MbedTLS.HMAC sha1 = CreateHMAC(user_key);
                byte[] sha1data = sha1.ComputeHash(outdata, 0, outlength - 4);
                Array.Copy(sha1data, 0, outdata, outlength - 4, 4);
            }
        }

        // plaindata == null    try send buffer data, return null if empty buffer
        // datalength == 0      sendback, return 0
        // datalength == -1     keepalive
        public override byte[] ClientPreEncrypt(byte[] plaindata, int datalength, out int outlength)
        {
            DateTime last = lastSendTime;
            DateTime now = DateTime.Now;
            lastSendTime = now;
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
                Sync();
                last = now;
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
            const int unit_len = 8100;
            int ogn_datalength = datalength;
            if (datalength < 0 || last != null && (now - last).TotalSeconds > 3)
            {
                Sync();
            }
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

                AddPacket(outlength);
                return outdata;
            }
            bool nopadding = false;

#if !PROTOCOL_STATISTICS
            if (datalength > 120 * 4 && pack_id < 32)
            {
                {
                    int send_len = LinearRandomInt(120 * 16);
                    if (send_len < datalength)
                    {
                        send_len = TrapezoidRandomInt(Math.Min(datalength - 1, Server.tcp_mss - overhead) - 1, -0.3) + 1;  // must less than datalength
#else
            if (datalength > 120 * 4 && pack_id < 64 )
            {
                int send_len = LinearRandomInt(datalength + 120 * 4);
                if (send_len < datalength)
                {
                    //long min_0 = tree.GetMin(0, 512);
                    //long min_512 = tree.GetMin(512, tree.Size);
                    //if (min_0 < min_512)
                    {
                        int max_packet_size = Math.Min(datalength - 1 + overhead, Server.tcp_mss); // must less than datalength + overhead
                        int len = GenRandLen(overhead + 1, max_packet_size) - overhead; // at least 1 byte data
                        send_len = len;
#endif

                        send_len = datalength - send_len;

                        if (send_len > 0)
                        {
                            send_buffer = new byte[send_len];
                            Array.Copy(data, datalength - send_len, send_buffer, 0, send_len);
                            datalength -= send_len;
                        }
                        nopadding = true;
                    }
                }
            }
            while (datalength > unit_len)
            {
                int outlen;
                PackData(data, unit_len, ogn_datalength, packdata, out outlen, nopadding);
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
                PackData(data, datalength, ogn_datalength, packdata, out outlen, nopadding);
                Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                Array.Copy(packdata, 0, outdata, outlength, outlen);
                outlength += outlen;
            }
            last_datalength = ogn_datalength;
            if (outlength > 0)
                AddPacket(outlength);
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
                MbedTLS.HMAC sha1 = CreateHMAC(key);
                {
                    byte[] sha1data = sha1.ComputeHash(recv_buf, 0, 2);
                    if (sha1data[0] != recv_buf[2] || sha1data[1] != recv_buf[3])
                    {
                        throw new ObfsException("ClientPostDecrypt data error");
                    }
                }

                int len = (recv_buf[1] << 8) + recv_buf[0];
                if (len >= 8192 || len < 8)
                {
                    throw new ObfsException("ClientPostDecrypt data error");
                }
                if (len > recv_buf_len)
                    break;

                {
                    byte[] sha1data = sha1.ComputeHash(recv_buf, 0, len - 4);
                    if (sha1data[0] != recv_buf[len - 4]
                        || sha1data[1] != recv_buf[len - 3]
                        || sha1data[2] != recv_buf[len - 2]
                        || sha1data[3] != recv_buf[len - 1]
                        )
                    {
                        throw new ObfsException("ClientPostDecrypt data uncorrect checksum");
                    }
                }

                {
                    ++recv_id;
                    int pos = recv_buf[4];
                    if (pos < 255)
                    {
                        pos += 4;
                    }
                    else
                    {
                        pos = ((recv_buf[6] << 8) | recv_buf[5]) + 4;
                    }
                    int outlen = len - pos - 4;
                    Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                    Array.Copy(recv_buf, pos, outdata, outlength, outlen);
                    outlength += outlen;
                    recv_buf_len -= len;
                    Array.Copy(recv_buf, len, recv_buf, 0, recv_buf_len);
                }
            }
            return outdata;
        }

        public override byte[] ClientUdpPreEncrypt(byte[] plaindata, int datalength, out int outlength)
        {
            byte[] outdata = new byte[datalength + 8];
            if (user_key == null)
            {
                user_id = new byte[4];
                int index_of_split = Server.param.IndexOf(':');
                if (index_of_split > 0)
                {
                    try
                    {
                        uint user = uint.Parse(Server.param.Substring(0, index_of_split));
                        user_key = hash(System.Text.Encoding.UTF8.GetBytes(Server.param.Substring(index_of_split + 1)));
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
            outlength = datalength + 8;
            Array.Copy(plaindata, 0, outdata, 0, datalength);
            user_id.CopyTo(outdata, datalength);
            {
                MbedTLS.HMAC sha1 = CreateHMAC(user_key);
                byte[] sha1data = sha1.ComputeHash(outdata, 0, outlength - 4);
                Array.Copy(sha1data, 0, outdata, outlength - 4, 4);
            }
            return outdata;
        }

        public override byte[] ClientUdpPostDecrypt(byte[] plaindata, int datalength, out int outlength)
        {
            if (datalength <= 4)
            {
                outlength = 0;
                return plaindata;
            }
            MbedTLS.HMAC sha1 = CreateHMAC(Server.key);
            byte[] sha1data = sha1.ComputeHash(plaindata, 0, datalength - 4);
            if (sha1data[0] != plaindata[datalength - 4]
                || sha1data[1] != plaindata[datalength - 3]
                || sha1data[2] != plaindata[datalength - 2]
                || sha1data[3] != plaindata[datalength - 1]
                )
            {
                outlength = 0;
                return plaindata;
            }
            outlength = datalength - 4;
            return plaindata;
        }

        protected override void Dispose(bool disposing)
        {
#if PROTOCOL_STATISTICS
            if (disposing)
            {
                if (Server != null && Server.data != null && packet_cnt != null)
                {
                    AuthDataAes128 authData = Server.data as AuthDataAes128;
                    if (authData != null && authData.tree != null)
                    {
                        lock (authData)
                        {
                            authData.tree.Update(packet_cnt);
                        }
                    }
                }
            }
#endif
        }
    }
}
