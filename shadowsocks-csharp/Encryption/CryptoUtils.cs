using CryptoBase.Abstractions.Digests;
using CryptoBase.Digests;
using CryptoBase.SymmetricCryptos.BlockCryptos.AES;
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

        public static byte[] MD5(ReadOnlySpan<byte> input)
        {
            var output = new byte[HashConstants.Md5Length];
            using var hash = DigestUtils.Create(DigestType.Md5);
            hash.UpdateFinal(input, output);
            return output;
        }

        public static byte[] SHA1(ReadOnlySpan<byte> input)
        {
            var output = new byte[HashConstants.Sha1Length];
            using var hash = DigestUtils.Create(DigestType.Sha1);
            hash.UpdateFinal(input, output);
            return output;
        }
    }
}
