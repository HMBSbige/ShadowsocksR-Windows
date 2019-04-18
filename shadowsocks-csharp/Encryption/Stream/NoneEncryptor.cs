﻿using System;
using System.Collections.Generic;

namespace Shadowsocks.Encryption.Stream
{
    public sealed class NoneEncryptor : StreamEncryptor
    {
        public NoneEncryptor(string method, string password) : base(method, password)
        {

        }

        private static Dictionary<string, EncryptorInfo> _ciphers = new Dictionary<string, EncryptorInfo>
        {
                {"none", new EncryptorInfo(16, 0, true, 1)},
        };

        public static List<string> SupportedCiphers()
        {
            return new List<string>(_ciphers.Keys);
        }

        protected override Dictionary<string, EncryptorInfo> getCiphers()
        {
            return _ciphers;
        }

        protected override void cipherUpdate(bool isCipher, int length, byte[] buf, byte[] outbuf)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(this.ToString());
            }

            Array.Copy(buf, outbuf, length);
        }


        #region IDisposable

        private bool _disposed;

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~NoneEncryptor()
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
        }

        #endregion
    }
}
