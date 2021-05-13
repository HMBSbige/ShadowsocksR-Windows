using System;

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
}
