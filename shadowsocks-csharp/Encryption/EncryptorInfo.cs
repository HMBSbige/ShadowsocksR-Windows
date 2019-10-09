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
}