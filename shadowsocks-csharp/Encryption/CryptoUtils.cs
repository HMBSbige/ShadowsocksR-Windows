using CryptoBase;
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
    }
}
