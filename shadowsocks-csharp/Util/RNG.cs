using System;
using System.Security.Cryptography;

namespace Shadowsocks.Util
{
    public static class Rng
    {
        public static void RandBytes(byte[] buf, int length = -1)
        {
            if (length == -1)
            {
                length = buf.Length;
            }

            var temp = new byte[length];
            using (var rngServiceProvider = new RNGCryptoServiceProvider())
            {
                rngServiceProvider.GetBytes(temp);
            }

            temp.CopyTo(buf, 0);
        }

        public static uint RandUInt32()
        {
            var temp = new byte[4];
            using (var rngServiceProvider = new RNGCryptoServiceProvider())
            {
                rngServiceProvider.GetBytes(temp);
            }

            return BitConverter.ToUInt32(temp, 0);
        }

        public static string RandId()
        {
            return Guid.NewGuid().ToString().Replace(@"-", string.Empty);
        }
    }
}
