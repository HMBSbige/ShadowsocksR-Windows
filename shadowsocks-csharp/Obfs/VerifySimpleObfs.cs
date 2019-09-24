using Shadowsocks.Controller;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;

namespace Shadowsocks.Obfs
{
    public class VerifyData
    {
    }

    public abstract class VerifySimpleBase : ObfsBase
    {
        public VerifySimpleBase(string method)
            : base(method)
        {
        }

        protected const int RecvBufferSize = 65536 * 2;

        protected byte[] recv_buf = new byte[RecvBufferSize];
        protected int recv_buf_len;
        protected Random random = new Random();

        public override object InitData()
        {
            return new VerifyData();
        }

        public int LinearRandomInt(int max)
        {
            return random.Next(max);
        }

        public int NonLinearRandomInt(int max)
        {
            int r1, r2;
            if ((max & 1) == 1)
            {
                var mid = (max + 1) >> 1;
                r1 = random.Next(mid);
                r2 = random.Next(mid + 1);
                var r = r1 + r2;
                if (r == max) return mid - 1;
                if (r < mid) return mid - r - 1;
                return max - r + mid - 1;
            }
            else
            {
                var mid = max >> 1;
                r1 = random.Next(mid);
                r2 = random.Next(mid + 1);
                var r = r1 + r2;
                if (r < mid) return mid - r - 1;
                return max - r + mid - 1;
            }
        }

        public double TrapezoidRandomFloat(double d) // －1 <= d <= 1
        {
            if (Math.Abs(d) < 0.000001)
                return random.NextDouble();

            var s = random.NextDouble();
            //(2dx + 2(1 - d))x/2 = s
            //dx^2 + (1-d)x - s = 0
            var a = 1 - d;
            //dx^2 + ax - s = 0
            //[-a + sqrt(a^2 + 4ds)] / 2d
            return (Math.Sqrt(a * a + 4 * d * s) - a) / (2 * d);
        }

        public int TrapezoidRandomInt(int max, double d)
        {
            var v = TrapezoidRandomFloat(d);
            return (int)(v * max);
        }

        public override byte[] ClientEncode(byte[] encryptdata, int datalength, out int outlength)
        {
            outlength = datalength;
            return encryptdata;
        }

        public override byte[] ClientDecode(byte[] encryptdata, int datalength, out int outlength, out bool needsendback)
        {
            outlength = datalength;
            needsendback = false;
            return encryptdata;
        }
    }

    public class VerifyDeflateObfs : VerifySimpleBase
    {
        public VerifyDeflateObfs(string method)
            : base(method)
        {
        }
        private static Dictionary<string, int[]> _obfs = new Dictionary<string, int[]> {
                {"verify_deflate", new[]{1, 0, 1}}
        };

        public static List<string> SupportedObfs()
        {
            return new List<string>(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        public void PackData(byte[] data, int datalength, byte[] outdata, out int outlength)
        {
            var comdata = FileManager.DeflateCompress(data, 0, datalength, out var outlen);
            outlength = outlen + 2 + 4;
            outdata[0] = (byte)(outlength >> 8);
            outdata[1] = (byte)outlength;
            Array.Copy(comdata, 0, outdata, 2, outlen);
            var adler = Adler32.CalcAdler32(data, datalength);
            outdata[outlength - 4] = (byte)(adler >> 24);
            outdata[outlength - 3] = (byte)(adler >> 16);
            outdata[outlength - 2] = (byte)(adler >> 8);
            outdata[outlength - 1] = (byte)adler;
        }

        public override byte[] ClientPreEncrypt(byte[] plaindata, int datalength, out int outlength)
        {
            if (plaindata == null)
            {
                outlength = 0;
                return null;
            }
            var outdata = new byte[datalength + datalength / 10 + 32];
            var packdata = new byte[32768];
            var data = plaindata;
            outlength = 0;
            const int unit_len = 32700;
            while (datalength > unit_len)
            {
                PackData(data, unit_len, packdata, out var outlen);
                Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                Array.Copy(packdata, 0, outdata, outlength, outlen);
                outlength += outlen;
                datalength -= unit_len;
                var newdata = new byte[datalength];
                Array.Copy(data, unit_len, newdata, 0, newdata.Length);
                data = newdata;
            }
            if (datalength > 0)
            {
                PackData(data, datalength, packdata, out var outlen);
                Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                Array.Copy(packdata, 0, outdata, outlength, outlen);
                outlength += outlen;
            }
            return outdata;
        }

        public override byte[] ClientPostDecrypt(byte[] plaindata, int datalength, out int outlength)
        {
            var outdata = new byte[recv_buf_len + datalength * 2 + 16];
            Array.Copy(plaindata, 0, recv_buf, recv_buf_len, datalength);
            recv_buf_len += datalength;
            outlength = 0;
            while (recv_buf_len > 2)
            {
                var len = (recv_buf[0] << 8) + recv_buf[1];
                if (len >= 32768 || len < 6)
                {
                    throw new ObfsException("ClientPostDecrypt data error");
                }
                if (len > recv_buf_len)
                    break;

                var buf = FileManager.DeflateDecompress(recv_buf, 2, len - 6, out var outlen);
                if (buf != null)
                {
                    var alder = Adler32.CalcAdler32(buf, outlen);
                    if (recv_buf[len - 4] == (byte)(alder >> 24)
                        && recv_buf[len - 3] == (byte)(alder >> 16)
                        && recv_buf[len - 2] == (byte)(alder >> 8)
                        && recv_buf[len - 1] == (byte)alder)
                    {
                        //pass
                    }
                    else
                    {
                        throw new ObfsException("ClientPostDecrypt data decompress ERROR");
                    }
                    Utils.SetArrayMinSize2(ref outdata, outlength + outlen);
                    Array.Copy(buf, 0, outdata, outlength, outlen);
                    outlength += outlen;
                    recv_buf_len -= len;
                    Array.Copy(recv_buf, len, recv_buf, 0, recv_buf_len);
                }
                else
                {
                    throw new ObfsException("ClientPostDecrypt data decompress ERROR");
                }
            }
            return outdata;
        }
    }

}
