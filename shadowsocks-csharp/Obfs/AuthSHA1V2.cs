using CryptoBase.Digests;
using CryptoBase.Macs.Hmac;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Shadowsocks.Obfs
{
    public class AuthSHA1V2 : VerifySimpleBase
    {
        public AuthSHA1V2(string method)
                : base(method)
        {
            has_sent_header = false;
            has_recv_header = false;
        }
        private static Dictionary<string, int[]> _obfs = new()
        {
            { "auth_sha1_v2", new[] { 1, 0, 1 } }
        };

        protected bool has_sent_header;
        protected bool has_recv_header;

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
            var rand_len = (datalength >= 1300 ? 0 : datalength > 400 ? LinearRandomInt(128) : LinearRandomInt(1024)) + 1;
            outlength = rand_len + datalength + 6;
            if (datalength > 0)
            {
                Array.Copy(data, 0, outdata, rand_len + 2, datalength);
            }

            outdata[0] = (byte)(outlength >> 8);
            outdata[1] = (byte)outlength;
            {
                var rnd_data = new byte[rand_len];
                random.NextBytes(rnd_data);
                rnd_data.CopyTo(outdata, 2);
            }
            if (rand_len < 128)
            {
                outdata[2] = (byte)rand_len;
            }
            else
            {
                outdata[2] = 0xFF;
                outdata[3] = (byte)(rand_len >> 8);
                outdata[4] = (byte)rand_len;
            }
            var adler = Util.Adler32.CalcAdler32(outdata, outlength - 4);
            BitConverter.GetBytes((uint)adler).CopyTo(outdata, outlength - 4);
        }

        public void PackAuthData(byte[] data, int datalength, byte[] outdata, out int outlength)
        {
            var rand_len = (datalength > 400 ? LinearRandomInt(128) : LinearRandomInt(1024)) + 1;
            var data_offset = rand_len + 4 + 2;
            outlength = data_offset + datalength + 12 + 10;
            var authData = (AuthData)Server.data;
            {
                var rnd_data = new byte[rand_len];
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
                    authData.clientID = RandomNumberGenerator.GetBytes(8);
                    authData.connectionID = (uint)BitConverter.ToInt64(authData.clientID, 0) % 0xFFFFFD;
                }
                authData.connectionID += 1;
                Array.Copy(authData.clientID, 0, outdata, data_offset, 8);
                Array.Copy(BitConverter.GetBytes(authData.connectionID), 0, outdata, data_offset + 8, 4);
            }

            Array.Copy(data, 0, outdata, data_offset + 12, datalength);
            outdata[4] = (byte)(outlength >> 8);
            outdata[5] = (byte)outlength;
            if (rand_len < 128)
            {
                outdata[6] = (byte)rand_len;
            }
            else
            {
                outdata[6] = 0xFF;
                outdata[7] = (byte)(rand_len >> 8);
                outdata[8] = (byte)rand_len;
            }

            var salt = System.Text.Encoding.UTF8.GetBytes("auth_sha1_v2");
            var crcdata = new byte[salt.Length + Server.key.Length];
            salt.CopyTo(crcdata, 0);
            Server.key.CopyTo(crcdata, salt.Length);
            var crc32 = Util.CRC32.CalcCRC32(crcdata, crcdata.Length);
            BitConverter.GetBytes((uint)crc32).CopyTo(outdata, 0);

            Span<byte> key = new byte[Server.Iv.Length + Server.key.Length];
            Server.Iv.AsSpan().CopyTo(key);
            Server.key.AsSpan().CopyTo(key[Server.Iv.Length..]);

            using var hmac = HmacUtils.Create(DigestType.Sha1, key);
            hmac.Update(outdata.AsSpan(0, outlength - 10));
            Span<byte> hash = stackalloc byte[hmac.Length];
            hmac.GetMac(hash);

            hash[..10].CopyTo(outdata.AsSpan(outlength - 10, 10));
        }

        public override byte[] ClientPreEncrypt(byte[] plaindata, int datalength, out int outlength)
        {
            if (plaindata == null)
            {
                outlength = 0;
                return null;
            }
            var outdata = new byte[datalength + datalength / 10 + 32];
            var packdata = new byte[9000];
            var data = plaindata;
            outlength = 0;
            const int unit_len = 8100;
            var ogn_datalength = datalength;
            if (!has_sent_header)
            {
                var headsize = GetHeadSize(plaindata, 30);
                var _datalength = Math.Min(LinearRandomInt(32) + headsize, datalength);
                PackAuthData(data, _datalength, packdata, out var outlen);
                has_sent_header = true;
                Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                Array.Copy(packdata, 0, outdata, outlength, outlen);
                outlength += outlen;
                datalength -= _datalength;
                var newdata = new byte[datalength];
                Array.Copy(data, _datalength, newdata, 0, newdata.Length);
                data = newdata;
            }
            while (datalength > unit_len)
            {
                PackData(data, unit_len, packdata, out var outlen);
                Util.Utils.SetArrayMinSize(ref outdata, outlength + outlen);
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

                PackData(data, datalength, packdata, out var outlen);
                Util.Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                Array.Copy(packdata, 0, outdata, outlength, outlen);
                outlength += outlen;
            }
            return outdata;
        }

        public override byte[] ClientPostDecrypt(byte[] plaindata, int datalength, out int outlength)
        {
            var outdata = new byte[recv_buf_len + datalength];
            Array.Copy(plaindata, 0, recv_buf, recv_buf_len, datalength);
            recv_buf_len += datalength;
            outlength = 0;
            while (recv_buf_len > 2)
            {
                var len = (recv_buf[0] << 8) + recv_buf[1];
                if (len is >= 8192 or < 8)
                {
                    throw new ObfsException("ClientPostDecrypt data error");
                }
                if (len > recv_buf_len)
                {
                    break;
                }

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
                    var outlen = len - pos - 4;
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
}
