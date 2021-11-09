using CryptoBase.Abstractions;
using CryptoBase.Abstractions.Digests;
using CryptoBase.Digests;
using CryptoBase.Macs.Hmac;
using Shadowsocks.Controller;
using Shadowsocks.Encryption;
using Shadowsocks.Enums;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Shadowsocks.Obfs
{
    public class AuthAES128SHA1 : VerifySimpleBase
    {
        protected delegate byte[] hash_func(ReadOnlySpan<byte> input);

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
            {
                hash = CryptoUtils.MD5;
            }
            else
            {
                hash = CryptoUtils.SHA1;
            }

            random = new Random(RandomNumberGenerator.GetInt32(int.MaxValue));
        }
        private static Dictionary<string, int[]> _obfs = new()
        {
            { "auth_aes128_md5", new[] { 1, 0, 1 } },
            { "auth_aes128_sha1", new[] { 1, 0, 1 } }
        };

        protected bool has_sent_header;
        protected bool has_recv_header;
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
        protected Dictionary<int, long> packet_cnt = new();
        //protected int[] packet_mul;
        protected Model.MinSearchTree tree;
        protected const int tree_offset = 9;
        protected DateTime lastSendTime;

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

        protected IMac CreateHMAC(byte[] key)
        {
            return Method switch
            {
                @"auth_aes128_md5" => HmacUtils.Create(DigestType.Md5, key),
                @"auth_aes128_sha1" => HmacUtils.Create(DigestType.Sha1, key),
                _ => null
            };
        }

        protected void Sync()
        {
#if PROTOCOL_STATISTICS
            if (Server.data is not null and AuthDataAes128 authData)
            {
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
            var len = tree.RandomFindIndex(dataLengthMin - 1, dataLengthMax, random);
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
                if (length >= tree.Size)
                {
                    length = tree.Size - 1;
                }

                lock (Server.data)
                {
                    if (packet_cnt.ContainsKey(length))
                    {
                        packet_cnt[length] += 1;
                    }
                    else
                    {
                        packet_cnt[length] = 1;
                    }
                }
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
            {
                return 0;
            }

            var rev_len = Server.tcp_mss - datalength - overhead;
            if (rev_len <= 0)
            {
                return 0;
            }

            if (datalength > 1100)
            {
                return LinearRandomInt(rev_len);
            }

            return TrapezoidRandomInt(rev_len, -0.3);
        }

#if PROTOCOL_STATISTICS
        // packetlength + padding = real_packetlength
        // return size of padding, at least 1
        protected int GenRandLenFull(int packetlength, int fulldatalength, bool nopadding)
        {
            if (nopadding || fulldatalength >= Server.buffer_size)
            {
                return packetlength;
            }

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
            const int overHead = 8;
            var rand_len = GenRandLenFull((datalength == 0 ? 1 : datalength) + overHead + 1, fulldatalength, nopadding)
                           - datalength - overHead;
#endif
            outlength = rand_len + datalength + 8;
            if (datalength > 0)
            {
                Array.Copy(data, 0, outdata, rand_len + 4, datalength);
            }

            outdata[0] = (byte)outlength;
            outdata[1] = (byte)(outlength >> 8);
            var key = new byte[user_key.Length + 4];
            user_key.CopyTo(key, 0);
            BitConverter.GetBytes(pack_id).CopyTo(key, key.Length - 4);
            {
                var rnd_data = new byte[rand_len];
                random.NextBytes(rnd_data);
                rnd_data.CopyTo(outdata, 4);
            }

            using var sha1 = CreateHMAC(key);
            {
                sha1.Update(outdata.AsSpan(0, 2));
                Span<byte> span = stackalloc byte[sha1.Length];
                sha1.GetMac(span);
                span[..2].CopyTo(outdata.AsSpan(2, 2));
            }
            if (rand_len < 128)
            {
                outdata[4] = (byte)rand_len;
            }
            else
            {
                outdata[4] = 0xFF;
                outdata[5] = (byte)rand_len;
                outdata[6] = (byte)(rand_len >> 8);
            }
            ++pack_id;
            {
                sha1.Update(outdata.AsSpan(0, outlength - 4));
                Span<byte> span = stackalloc byte[sha1.Length];
                sha1.GetMac(span);
                span[..4].CopyTo(outdata.AsSpan(outlength - 4, 4));
            }
        }

        public void PackAuthData(byte[] data, int datalength, byte[] outdata, out int outlength)
        {
            const int authHeadLen = 7 + 4 + 16 + 4;
            const int overHead = authHeadLen + 4;
            var encrypt = new byte[24];

            if (Server.data is AuthDataAes128 authData)
            {
                lock (authData)
                {
                    if (authData.connectionID > 0xFF000000)
                    {
                        authData.clientID = null;
                    }

                    if (authData.clientID == null)
                    {
                        authData.clientID = RandomNumberGenerator.GetBytes(4);
                        authData.connectionID = (uint)BitConverter.ToInt32(authData.clientID, 0) % 0xFFFFFD;
                    }

                    authData.connectionID += 1;
                    Array.Copy(authData.clientID, 0, encrypt, 4, 4);
                    Array.Copy(BitConverter.GetBytes(authData.connectionID), 0, encrypt, 8, 4);

                    StatisticsInit(authData);
                }
            }

#if !PROTOCOL_STATISTICS
            int rand_len = TrapezoidRandomInt(Server.tcp_mss - datalength - overhead + 1, -0.3); //(datalength > 400 ? LinearRandomInt(512) : LinearRandomInt(1024));
#else
            var rand_len = GenRandLenFull(datalength + overHead, datalength, false) - datalength - overHead;
#endif
            var data_offset = rand_len + authHeadLen;
            outlength = data_offset + datalength + 4;
            var encrypt_data = new byte[32];
            var key = new byte[Server.Iv.Length + Server.key.Length];
            Server.Iv.CopyTo(key, 0);
            Server.key.CopyTo(key, Server.Iv.Length);

            {
                var rnd_data = new byte[rand_len];
                random.NextBytes(rnd_data);
                rnd_data.CopyTo(outdata, data_offset - rand_len);
            }

            var utc_time_second = (ulong)Math.Floor(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);
            var utc_time = (uint)utc_time_second;
            Array.Copy(BitConverter.GetBytes(utc_time), 0, encrypt, 0, 4);
            encrypt[12] = (byte)outlength;
            encrypt[13] = (byte)(outlength >> 8);
            encrypt[14] = (byte)rand_len;
            encrypt[15] = (byte)(rand_len >> 8);

            {
                var uid = new byte[4];
                var index_of_split = Server.param.IndexOf(':');
                if (index_of_split > 0)
                {
                    try
                    {
                        var user = uint.Parse(Server.param.Substring(0, index_of_split));
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

                var encrypt_key = user_key;

                CryptoUtils.SsAes128(Convert.ToBase64String(encrypt_key) + SALT, encrypt.AsSpan(0, 16), encrypt_data.AsSpan(0, 16));
                Array.Copy(encrypt_data, 0, encrypt, 4, 16);
                uid.CopyTo(encrypt, 0);
            }
            {
                using var sha1 = CreateHMAC(key);
                sha1.Update(encrypt.AsSpan(0, 20));
                Span<byte> span = stackalloc byte[sha1.Length];
                sha1.GetMac(span);
                span[..4].CopyTo(encrypt.AsSpan(20, 4));

                var rnd = new byte[1];
                random.NextBytes(rnd);
                rnd.CopyTo(outdata, 0);

                sha1.Update(rnd);
                sha1.GetMac(span);
                span[..(7 - rnd.Length)].CopyTo(outdata.AsSpan(rnd.Length, 7 - rnd.Length));
            }
            encrypt.CopyTo(outdata, 7);
            Array.Copy(data, 0, outdata, data_offset, datalength);

            {
                using var sha1 = CreateHMAC(user_key);
                sha1.Update(outdata.AsSpan(0, outlength - 4));
                Span<byte> span = stackalloc byte[sha1.Length];
                sha1.GetMac(span);
                span[..4].CopyTo(outdata.AsSpan(outlength - 4, 4));
            }
        }

        // plaindata == null    try send buffer data, return null if empty buffer
        // datalength == 0      sendback, return 0
        // datalength == -1     keepalive
        public override byte[] ClientPreEncrypt(byte[] plaindata, int datalength, out int outlength)
        {
            var last = lastSendTime;
            var now = DateTime.Now;
            lastSendTime = now;
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
                Sync();
                last = now;
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
            const int unit_len = 8100;
            var ogn_datalength = datalength;
            if (datalength < 0 || (now - last).TotalSeconds > 3)
            {
                Sync();
            }
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

                AddPacket(outlength);
                return outdata;
            }
            var nopadding = false;

#if !PROTOCOL_STATISTICS
            if (datalength > 120 * 4 && pack_id < 32)
            {
                {
                    int send_len = LinearRandomInt(120 * 16);
                    if (send_len < datalength)
                    {
                        send_len = TrapezoidRandomInt(Math.Min(datalength - 1, Server.tcp_mss - overhead) - 1, -0.3) + 1;  // must less than datalength
#else
            if (datalength > 120 * 4 && pack_id < 64)
            {
                var send_len = LinearRandomInt(datalength + 120 * 4);
                if (send_len < datalength)
                {
                    //long min_0 = tree.GetMin(0, 512);
                    //long min_512 = tree.GetMin(512, tree.Size);
                    //if (min_0 < min_512)
                    {
                        var max_packet_size = Math.Min(datalength - 1 + overhead, Server.tcp_mss); // must less than datalength + overhead
                        var len = GenRandLen(overhead + 1, max_packet_size) - overhead; // at least 1 byte data
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
                PackData(data, unit_len, ogn_datalength, packdata, out var outlen, nopadding);
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
                {
                    datalength = 0;
                }

                PackData(data, datalength, ogn_datalength, packdata, out var outlen, nopadding);
                Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                Array.Copy(packdata, 0, outdata, outlength, outlen);
                outlength += outlen;
            }
            last_datalength = ogn_datalength;
            if (outlength > 0)
            {
                AddPacket(outlength);
            }

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

            Span<byte> span = stackalloc byte[HashConstants.Sha1Length];
            while (recv_buf_len > 4)
            {
                BitConverter.GetBytes(recv_id).CopyTo(key, key.Length - 4);
                using var sha1 = CreateHMAC(key);
                {
                    sha1.Update(recv_buf.AsSpan(0, 2));
                    sha1.GetMac(span);
                    if (span[0] != recv_buf[2] || span[1] != recv_buf[3])
                    {
                        throw new ObfsException("ClientPostDecrypt data error");
                    }
                }

                var len = (recv_buf[1] << 8) + recv_buf[0];
                if (len is >= 8192 or < 8)
                {
                    throw new ObfsException("ClientPostDecrypt data error");
                }
                if (len > recv_buf_len)
                {
                    break;
                }

                sha1.Update(recv_buf.AsSpan(0, len - 4));
                sha1.GetMac(span);

                if (!span[..4].SequenceEqual(recv_buf.AsSpan(len - 4, 4)))
                {
                    throw new ObfsException("ClientPostDecrypt data uncorrect checksum");
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
                    var outlen = len - pos - 4;
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
            var outdata = new byte[datalength + 8];
            if (user_key == null)
            {
                user_id = new byte[4];
                var index_of_split = Server.param.IndexOf(':');
                if (index_of_split > 0)
                {
                    try
                    {
                        var user = uint.Parse(Server.param.Substring(0, index_of_split));
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
                using var sha1 = CreateHMAC(user_key);
                sha1.Update(outdata.AsSpan(0, outlength - 4));
                Span<byte> span = stackalloc byte[sha1.Length];
                sha1.GetMac(span);
                span[..4].CopyTo(outdata.AsSpan(outlength - 4, 4));
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
            using var sha1 = CreateHMAC(Server.key);
            sha1.Update(plaindata.AsSpan(0, datalength - 4));
            Span<byte> span = stackalloc byte[sha1.Length];
            sha1.GetMac(span);

            if (span[..4].SequenceEqual(plaindata.AsSpan(datalength - 4, 4)))
            {
                outlength = datalength - 4;
                return plaindata;
            }

            outlength = 0;
            return plaindata;
        }

        protected override void Dispose(bool disposing)
        {
#if PROTOCOL_STATISTICS
            if (disposing)
            {
                if (Server != null && Server.data != null && packet_cnt != null)
                {
                    var authData = Server.data as AuthDataAes128;
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
