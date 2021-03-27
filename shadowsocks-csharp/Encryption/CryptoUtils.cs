using CryptoBase;
using CryptoBase.Digests.MD5;
using CryptoBase.Digests.SHA1;
using Shadowsocks.Crypto;
using System;
using System.Buffers;

namespace Shadowsocks.Encryption
{
    public static class CryptoUtils
    {
        public static void SsAes128(string password, ReadOnlySpan<byte> source, Span<byte> destination)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(16);
            try
            {
                buffer.AsSpan(0, 16).SsDeriveKey(password);
                using var aes = AESUtils.CreateECB(buffer);
                aes.Encrypt(source, destination);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static byte[] MD5(Span<byte> input)
        {
            var output = new byte[16];
            MD5Utils.Default(input, output);
            return output;
        }

        public static byte[] SHA1(Span<byte> input)
        {
            var output = new byte[20];
            SHA1Utils.Default(input, output);
            return output;
        }
    }
}
