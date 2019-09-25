﻿namespace Shadowsocks.Encryption
{
    public class EncryptorInfo
    {
        public int key_size;
        public int iv_size;
        public bool display;
        public int type;
        public string name;

        public EncryptorInfo(int key, int iv, bool display, int type, string name = "")
        {
            key_size = key;
            iv_size = iv;
            this.display = display;
            this.type = type;
            this.name = name;
        }
    }

    public abstract class EncryptorBase : IEncryptor
    {
        public const int MAX_INPUT_SIZE = 32768;

        public const int MD5_LEN = 16;

        public const int ADDR_PORT_LEN = 2;
        public const int ADDR_ATYP_LEN = 1;

        public const int ATYP_IPv4 = 0x01;
        public const int ATYP_DOMAIN = 0x03;
        public const int ATYP_IPv6 = 0x04;

        protected EncryptorBase(string method, string password)
        {
            Method = method;
            Password = password;
        }

        protected string Method;
        protected string Password;

        public abstract void Encrypt(byte[] buf, int length, byte[] outbuf, out int outlength);

        public abstract void Decrypt(byte[] buf, int length, byte[] outbuf, out int outlength);
        public abstract void ResetEncrypt();
        public abstract void ResetDecrypt();

        public abstract void Dispose();
        public abstract byte[] getIV();
        public abstract byte[] getKey();
        public abstract EncryptorInfo getInfo();
    }
}
