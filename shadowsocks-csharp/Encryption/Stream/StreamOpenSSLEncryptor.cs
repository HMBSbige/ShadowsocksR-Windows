﻿using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Shadowsocks.Encryption.Stream
{
    public sealed class StreamOpenSSLEncryptor : StreamEncryptor
    {
        const int CIPHER_AES = 1;
        const int CIPHER_RC4 = 2;
        const int CIPHER_CAMELLIA = 3;
        const int CIPHER_OTHER_CFB = 4;

        private IntPtr _encryptCtx = IntPtr.Zero;
        private IntPtr _decryptCtx = IntPtr.Zero;

        public StreamOpenSSLEncryptor(string method, string password) : base(method, password)
        {

        }

        private static readonly Dictionary<string, EncryptorInfo> _ciphers = new Dictionary<string, EncryptorInfo> {
                {"aes-128-cbc", new EncryptorInfo(16, 16, false, CIPHER_AES)},
                {"aes-192-cbc", new EncryptorInfo(24, 16, false, CIPHER_AES)},
                {"aes-256-cbc", new EncryptorInfo(32, 16, false, CIPHER_AES)},

                {"aes-128-ctr", new EncryptorInfo(16, 16, true, CIPHER_AES)},
                {"aes-192-ctr", new EncryptorInfo(24, 16, true, CIPHER_AES)},
                {"aes-256-ctr", new EncryptorInfo(32, 16, true, CIPHER_AES)},
                {"aes-128-cfb", new EncryptorInfo(16, 16, true, CIPHER_AES)},
                {"aes-192-cfb", new EncryptorInfo(24, 16, true, CIPHER_AES)},
                {"aes-256-cfb", new EncryptorInfo(32, 16, true, CIPHER_AES)},

                {"aes-128-cfb8", new EncryptorInfo(16, 16, true, CIPHER_AES)},
                {"aes-192-cfb8", new EncryptorInfo(24, 16, true, CIPHER_AES)},
                {"aes-256-cfb8", new EncryptorInfo(32, 16, true, CIPHER_AES)},
                {"aes-128-cfb1", new EncryptorInfo(16, 16, false, CIPHER_AES)},
                {"aes-192-cfb1", new EncryptorInfo(24, 16, false, CIPHER_AES)},
                {"aes-256-cfb1", new EncryptorInfo(32, 16, false, CIPHER_AES)},
                {"camellia-128-cfb", new EncryptorInfo(16, 16, false, CIPHER_CAMELLIA)},
                {"camellia-192-cfb", new EncryptorInfo(24, 16, false, CIPHER_CAMELLIA)},
                {"camellia-256-cfb", new EncryptorInfo(32, 16, false, CIPHER_CAMELLIA)},
                {"bf-cfb", new EncryptorInfo(16, 8, false, CIPHER_OTHER_CFB)},
                {"cast5-cfb", new EncryptorInfo(16, 8, false, CIPHER_OTHER_CFB)},
                {"idea-cfb", new EncryptorInfo(16, 8, false, CIPHER_OTHER_CFB)},
                {"rc2-cfb", new EncryptorInfo(16, 8, false, CIPHER_OTHER_CFB)},
                {"rc4", new EncryptorInfo(16, 0, true, CIPHER_RC4)}, // weak
                {"rc4-md5", new EncryptorInfo(16, 16, true, CIPHER_RC4, "RC4")}, // weak
                {"rc4-md5-6", new EncryptorInfo(16, 6, true, CIPHER_RC4, "RC4")}, // weak
                {"seed-cfb", new EncryptorInfo(16, 16, false, CIPHER_OTHER_CFB)}
        };

        public static IEnumerable<string> SupportedCiphers()
        {
            return new List<string>(_ciphers.Keys);
        }

        protected override Dictionary<string, EncryptorInfo> getCiphers()
        {
            return _ciphers;
        }

        protected override void initCipher(byte[] iv, bool isCipher)
        {
            base.initCipher(iv, isCipher);

            IntPtr cipherInfo = OpenSSL.GetCipherInfo(_innerLibName);
            if (cipherInfo == IntPtr.Zero) throw new Exception("openssl: cipher not found");
            IntPtr ctx = OpenSSL.EVP_CIPHER_CTX_new();
            if (ctx == IntPtr.Zero) throw new Exception("fail to create ctx");

            if (isCipher)
            {
                _encryptCtx = ctx;
            }
            else
            {
                _decryptCtx = ctx;
            }

            byte[] realKey;
            if (_method.StartsWith(@"rc4-md5"))
            {
                byte[] temp = new byte[keyLen + ivLen];
                Array.Copy(_key, 0, temp, 0, keyLen);
                Array.Copy(iv, 0, temp, keyLen, ivLen);
                realKey = MbedTLS.MD5(temp);
            }
            else
            {
                realKey = _key;
            }

            var ret = OpenSSL.EVP_CipherInit_ex(ctx, cipherInfo, IntPtr.Zero, null, null, isCipher ? OpenSSL.OPENSSL_ENCRYPT : OpenSSL.OPENSSL_DECRYPT);
            if (ret != 1) throw new Exception("openssl: fail to set key length");
            ret = OpenSSL.EVP_CIPHER_CTX_set_key_length(ctx, keyLen);
            if (ret != 1) throw new Exception("openssl: fail to set key length");
            ret = OpenSSL.EVP_CipherInit_ex(ctx, IntPtr.Zero, IntPtr.Zero, realKey, _method == "rc4-md5" ? null : iv, isCipher ? OpenSSL.OPENSSL_ENCRYPT : OpenSSL.OPENSSL_DECRYPT);
            if (ret != 1) throw new Exception("openssl: cannot set key and iv");
            OpenSSL.EVP_CIPHER_CTX_set_padding(ctx, 0);
        }

        protected override void cipherUpdate(bool isCipher, int length, byte[] buf, byte[] outbuf)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(ToString());
            }

            var ret = OpenSSL.EVP_CipherUpdate(isCipher ? _encryptCtx : _decryptCtx, outbuf, out var outlen, buf, length);
            if (ret != 1)
            {
                throw new Exception($@"ret is {ret}");
            }

            Debug.Assert(outlen == length);
        }

        #region IDisposable
        private bool _disposed;

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~StreamOpenSSLEncryptor()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            lock (this)
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
                OpenSSL.EVP_CIPHER_CTX_free(_encryptCtx);
                _encryptCtx = IntPtr.Zero;
            }

            if (_decryptCtx != IntPtr.Zero)
            {
                OpenSSL.EVP_CIPHER_CTX_free(_decryptCtx);
                _decryptCtx = IntPtr.Zero;
            }
        }
        #endregion
    }
}
