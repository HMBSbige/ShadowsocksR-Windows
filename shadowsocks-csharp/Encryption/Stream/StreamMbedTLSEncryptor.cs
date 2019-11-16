using Shadowsocks.Encryption.Exception;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Shadowsocks.Encryption.Stream
{
    public sealed class StreamMbedTLSEncryptor : StreamEncryptor
    {
        const int CIPHER_RC4 = 1;
        const int CIPHER_AES = 2;
        const int CIPHER_BLOWFISH = 3;
        const int CIPHER_CAMELLIA = 4;

        private IntPtr _encryptCtx = IntPtr.Zero;
        private IntPtr _decryptCtx = IntPtr.Zero;

        public StreamMbedTLSEncryptor(string method, string password) : base(method, password)
        {
        }

        private static readonly Dictionary<string, EncryptorInfo> _ciphers = new Dictionary<string, EncryptorInfo> {
            { @"aes-128-cbc", new EncryptorInfo(16, 16, CIPHER_AES, @"AES-128-CBC", false) },
            { @"aes-192-cbc", new EncryptorInfo(24, 16, CIPHER_AES, @"AES-192-CBC", false) },
            { @"aes-256-cbc", new EncryptorInfo(32, 16, CIPHER_AES, @"AES-256-CBC", false) },

            { @"aes-128-ctr", new EncryptorInfo(16, 16, CIPHER_AES, @"AES-128-CTR") },
            { @"aes-192-ctr", new EncryptorInfo(24, 16, CIPHER_AES, @"AES-192-CTR") },
            { @"aes-256-ctr", new EncryptorInfo(32, 16, CIPHER_AES, @"AES-256-CTR") },
            { @"aes-128-cfb", new EncryptorInfo(16, 16, CIPHER_AES, @"AES-128-CFB128") },
            { @"aes-192-cfb", new EncryptorInfo(24, 16, CIPHER_AES, @"AES-192-CFB128") },
            { @"aes-256-cfb", new EncryptorInfo(32, 16, CIPHER_AES, @"AES-256-CFB128") },
            { @"bf-cfb", new EncryptorInfo(16, 8, CIPHER_BLOWFISH, @"BLOWFISH-CFB64", false) },
            { @"camellia-128-cfb", new EncryptorInfo(16, 16, CIPHER_CAMELLIA, @"CAMELLIA-128-CFB128", false) },
            { @"camellia-192-cfb", new EncryptorInfo(24, 16, CIPHER_CAMELLIA, @"CAMELLIA-192-CFB128", false) },
            { @"camellia-256-cfb", new EncryptorInfo(32, 16, CIPHER_CAMELLIA, @"CAMELLIA-256-CFB128", false) },
            { @"rc4", new EncryptorInfo(16, 0, CIPHER_RC4, @"ARC4-128") },
            { @"rc4-md5", new EncryptorInfo(16, 16, CIPHER_RC4, @"ARC4-128") },
            { @"rc4-md5-6", new EncryptorInfo(16, 6, CIPHER_RC4, @"ARC4-128") }
        };

        public static List<string> SupportedCiphers()
        {
            return new List<string>(_ciphers.Keys);
        }

        protected override Dictionary<string, EncryptorInfo> getCiphers()
        {
            return _ciphers;
        }

        protected override void InitCipher(byte[] iv, bool isEncrypt)
        {
            base.InitCipher(iv, isEncrypt);
            var ctx = Marshal.AllocHGlobal(MbedTLS.cipher_get_size_ex());
            if (isEncrypt)
            {
                _encryptCtx = ctx;
            }
            else
            {
                _decryptCtx = ctx;
            }
            byte[] realKey;
            if (_method.StartsWith(@"rc4-"))
            {
                var temp = new byte[keyLen + ivLen];
                Array.Copy(_key, 0, temp, 0, keyLen);
                Array.Copy(iv, 0, temp, keyLen, ivLen);
                realKey = MbedTLS.MD5(temp);
            }
            else
            {
                realKey = _key;
            }
            MbedTLS.cipher_init(ctx);
            if (MbedTLS.cipher_setup(ctx, MbedTLS.cipher_info_from_string(getInfo().InnerLibName)) != 0)
            {
                throw new System.Exception("Cannot initialize mbed TLS cipher context");
            }
            /*
             * MbedTLS takes key length by bit
             * cipher_setkey() will set the correct key schedule
             * and operation
             *
             *  MBEDTLS_AES_{EN,DE}CRYPT
             *  == MBEDTLS_BLOWFISH_{EN,DE}CRYPT
             *  == MBEDTLS_CAMELLIA_{EN,DE}CRYPT
             *  == MBEDTLS_{EN,DE}CRYPT
             *  
             */
            if (MbedTLS.cipher_setkey(ctx, realKey, keyLen * 8,
                isEncrypt ? MbedTLS.MBEDTLS_ENCRYPT : MbedTLS.MBEDTLS_DECRYPT) != 0)
                throw new System.Exception("Cannot set mbed TLS cipher key");
            if (MbedTLS.cipher_set_iv(ctx, iv, ivLen) != 0)
                throw new System.Exception("Cannot set mbed TLS cipher IV");
            if (MbedTLS.cipher_reset(ctx) != 0)
                throw new System.Exception("Cannot finalize mbed TLS cipher context");
        }

        protected override void CipherUpdate(bool isCipher, int length, byte[] buf, byte[] outbuf)
        {
            // C# could be multi-threaded
            if (_disposed)
            {
                throw new ObjectDisposedException(ToString());
            }
            if (MbedTLS.cipher_update(isCipher ? _encryptCtx : _decryptCtx, buf, length, outbuf, ref length) != 0)
            {
                throw new CryptoErrorException("Cannot update mbed TLS cipher context");
            }
        }

        #region IDisposable

        private bool _disposed;

        // instance based lock
        private readonly object _lock = new object();

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~StreamMbedTLSEncryptor()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
            }

            if (disposing)
            {
                // free managed objects
            }

            // free unmanaged objects
            if (_encryptCtx != IntPtr.Zero)
            {
                MbedTLS.cipher_free(_encryptCtx);
                Marshal.FreeHGlobal(_encryptCtx);
                _encryptCtx = IntPtr.Zero;
            }
            if (_decryptCtx != IntPtr.Zero)
            {
                MbedTLS.cipher_free(_decryptCtx);
                Marshal.FreeHGlobal(_decryptCtx);
                _decryptCtx = IntPtr.Zero;
            }
        }

        #endregion
    }
}
