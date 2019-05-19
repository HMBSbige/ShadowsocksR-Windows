namespace Shadowsocks.Proxy
{
    public abstract class IHandler
    {
        public abstract void Shutdown();
    }

    public class CallbackState
    {
        public byte[] buffer;
        public int size;
        public int protocol_size;
        public object state;
    }
}
