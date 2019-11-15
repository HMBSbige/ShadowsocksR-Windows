using Shadowsocks.Encryption.CircularBuffer;
using Shadowsocks.Proxy;
using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace Shadowsocks.Encryption.Stream
{
    public abstract class StreamEncryptor : EncryptorBase
    {
        // every connection should create its own buffer
        private readonly ByteCircularBuffer _encCircularBuffer = new ByteCircularBuffer(ProxyAuthHandler.BufferSize * 2);
        private readonly ByteCircularBuffer _decCircularBuffer = new ByteCircularBuffer(ProxyAuthHandler.BufferSize * 2);

        protected Dictionary<string, EncryptorInfo> ciphers;

        protected byte[] _encryptIV;
        protected byte[] _decryptIV;

        // Is first packet
        private bool _decryptIVReceived;
        private bool _encryptIVSent;

        protected string _method;
        protected int _cipher;
        // internal name in the crypto library
        protected string _innerLibName;
        protected EncryptorInfo CipherInfo;
        // long-time master key
        protected byte[] _key;
        protected int keyLen;
        protected byte[] _iv;
        protected int ivLen;

        protected StreamEncryptor(string method, string password) : base(method, password)
        {
            InitEncryptorInfo(method);
            InitKey(password);
        }

        protected abstract Dictionary<string, EncryptorInfo> getCiphers();

        public bool SetIV(byte[] iv)
        {
            if (iv != null && iv.Length == ivLen)
            {
                iv.CopyTo(_iv, 0);
                _encryptIVSent = true;
                InitCipher(iv, true);
                return true;
            }
            return false;
        }
        public override byte[] getIV()
        {
            return _iv;
        }
        public override byte[] getKey()
        {
            var key = (byte[])_key.Clone();
            Array.Resize(ref key, keyLen);
            return key;
        }
        public override EncryptorInfo getInfo()
        {
            return CipherInfo;
        }

        private void InitEncryptorInfo(string method)
        {
            method = method.ToLower();
            _method = method;
            ciphers = getCiphers();
            CipherInfo = ciphers[_method];
            _innerLibName = string.IsNullOrWhiteSpace(CipherInfo.InnerLibName) ? method : CipherInfo.InnerLibName;
            _cipher = CipherInfo.Type;
            if (_cipher == 0)
            {
                throw new System.Exception("method not found");
            }

            keyLen = CipherInfo.KeySize;
            ivLen = CipherInfo.IvSize;
        }

        private void InitKey(string password)
        {
            var passBuf = Encoding.UTF8.GetBytes(password);
            if (_key == null) _key = new byte[keyLen];
            if (_key.Length != keyLen) Array.Resize(ref _key, keyLen);
            LegacyDeriveKey(passBuf, _key, keyLen);

            Array.Resize(ref _iv, ivLen);
            Rng.RandBytes(_iv, ivLen);
        }

        public static void LegacyDeriveKey(byte[] password, byte[] key, int keylen)
        {
            var result = new byte[password.Length + MD5_LEN];
            var i = 0;
            byte[] md5Sum = null;
            while (i < keylen)
            {
                if (i == 0)
                {
                    md5Sum = MbedTLS.MD5(password);
                }
                else
                {
                    if (md5Sum != null)
                    {
                        Array.Copy(md5Sum, 0, result, 0, MD5_LEN);
                        Array.Copy(password, 0, result, MD5_LEN, password.Length);
                    }
                    md5Sum = MbedTLS.MD5(result);
                }
                Array.Copy(md5Sum, 0, key, i, Math.Min(MD5_LEN, keylen - i));
                i += MD5_LEN;
            }
        }

        protected virtual void InitCipher(byte[] iv, bool isCipher)
        {
            if (isCipher)
            {
                _encryptIV = new byte[ivLen];
                Array.Copy(iv, _encryptIV, ivLen);
            }
            else
            {
                _decryptIV = new byte[ivLen];
                Array.Copy(iv, _decryptIV, ivLen);
            }
        }

        protected abstract void CipherUpdate(bool isCipher, int length, byte[] buf, byte[] outbuf);

        public override void Encrypt(byte[] buf, int length, byte[] outbuf, out int outlength)
        {
            //none rc4
            if (length == 0)
            {
                outlength = 0;
                return;
            }

            var cipherOffset = 0;
            _encCircularBuffer.Put(buf, 0, length);
            if (!_encryptIVSent)
            {
                InitCipher(_iv, true);
                Array.Copy(_iv, 0, outbuf, 0, ivLen);
                cipherOffset = ivLen;
                _encryptIVSent = true;
            }
            var size = _encCircularBuffer.Size;
            var plain = _encCircularBuffer.Get(size);
            var cipher = new byte[size];
            CipherUpdate(true, size, plain, cipher);
            Buffer.BlockCopy(cipher, 0, outbuf, cipherOffset, size);
            outlength = size + cipherOffset;
        }

        public override void Decrypt(byte[] buf, int length, byte[] outbuf, out int outlength)
        {
            _decCircularBuffer.Put(buf, 0, length);
            if (!_decryptIVReceived)
            {
                if (_decCircularBuffer.Size <= ivLen)
                {
                    // we need more data
                    outlength = 0;
                    return;
                }
                // start decryption
                _decryptIVReceived = true;
                var iv = ivLen == 0 ? new byte[0] : _decCircularBuffer.Get(ivLen); //none rc4
                InitCipher(iv, false);
            }
            var cipher = _decCircularBuffer.ToArray();
            CipherUpdate(false, cipher.Length, cipher, outbuf);
            // move pointer only
            _decCircularBuffer.Skip(_decCircularBuffer.Size);
            outlength = cipher.Length;
            // done the decryption
        }
    }

}
