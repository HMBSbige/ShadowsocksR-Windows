namespace Shadowsocks.Encryption
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

        public const int CHUNK_LEN_BYTES = 2;

        public const uint CHUNK_LEN_MASK = 0x3FFFu;

        public const int RecvSize = 2048;

        // overhead of one chunk, reserved for AEAD ciphers
        public const int ChunkOverheadSize = 16 * 2 /* two tags */ + CHUNK_LEN_BYTES;

        // max chunk size
        public const uint MaxChunkSize = CHUNK_LEN_MASK + CHUNK_LEN_BYTES + 16 * 2;

        // In general, the ciphertext length, we should take overhead into account
        public const int BufferSize = RecvSize + (int)MaxChunkSize + 32 /* max salt len */;

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
