using System;

namespace Shadowsocks.Obfs
{
    public interface IObfs : IDisposable
    {
        string Name();
        bool isKeepAlive();
        bool isAlwaysSendback();
        byte[] ClientPreEncrypt(byte[] plaindata, int datalength, out int outlength);
        byte[] ClientEncode(byte[] encryptdata, int datalength, out int outlength);
        byte[] ClientDecode(byte[] encryptdata, int datalength, out int outlength, out bool needsendback);
        byte[] ClientPostDecrypt(byte[] plaindata, int datalength, out int outlength);
        byte[] ClientUdpPreEncrypt(byte[] plaindata, int datalength, out int outlength);
        byte[] ClientUdpPostDecrypt(byte[] plaindata, int datalength, out int outlength);
        object InitData();
        void SetServerInfo(ServerInfo serverInfo);
        void SetServerInfoIV(byte[] iv);
        long GetSentLength();
        int GetOverhead();
        int GetTcpMSS();
    }
}
