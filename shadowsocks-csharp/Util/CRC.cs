using CryptoBase.Digests;
using System;
using System.Buffers.Binary;

namespace Shadowsocks.Util
{
    public static class CRC32
    {
        public static ulong CalcCRC32(byte[] input, int len)
        {
            using var hash = DigestUtils.Create(DigestType.Crc32);
            var t = new byte[hash.Length];
            hash.UpdateFinal(input.AsSpan(0, len), t);
            return BinaryPrimitives.ReadUInt32BigEndian(t);
        }

        public static void SetCRC32(byte[] buffer)
        {
            using var hash = DigestUtils.Create(DigestType.Crc32);
            var t = new byte[hash.Length];
            hash.UpdateFinal(buffer.AsSpan(0, buffer.Length - 4), t);
            var x = ~BinaryPrimitives.ReadUInt32BigEndian(t);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(buffer.Length - 4), x);
        }
    }

    public static class Adler32
    {
        public static ulong CalcAdler32(byte[] input, int len)
        {
            ulong a = 1;
            ulong b = 0;
            for (var i = 0; i < len; ++i)
            {
                a += input[i];
                b += a;
            }
            a %= 65521;
            b %= 65521;
            return (b << 16) + a;
        }

        public static bool CheckAdler32(byte[] input, int len)
        {
            var adler32 = CalcAdler32(input, len - 4);
            var checksum = BinaryPrimitives.ReadUInt32LittleEndian(input.AsSpan(len - 4));
            return (uint)adler32 == checksum;
        }
    }
}
