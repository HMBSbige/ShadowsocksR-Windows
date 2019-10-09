using System;

namespace Shadowsocks.Obfs
{
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
}