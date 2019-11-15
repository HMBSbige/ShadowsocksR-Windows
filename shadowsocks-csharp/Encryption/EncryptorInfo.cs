namespace Shadowsocks.Encryption
{
    public class EncryptorInfo
    {
        public int KeySize;
        public int IvSize;
        public bool Display;
        public int Type;
        public string InnerLibName;

        public EncryptorInfo(int key, int iv, bool display, int type, string innerLibName = "")
        {
            KeySize = key;
            IvSize = iv;
            Display = display;
            Type = type;
            InnerLibName = innerLibName;
        }
    }
}