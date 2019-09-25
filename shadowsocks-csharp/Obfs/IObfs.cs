﻿using System;

namespace Shadowsocks.Obfs
{
    public class ObfsException : Exception
    {
        public ObfsException(string info)
            : base(info)
        {

        }
    }
    public class ServerInfo
    {
        public string host;
        public int port;
        public string param;
        public object data;
        public int tcp_mss;
        public int overhead;
        public int buffer_size;
        public byte[] Iv;
        public byte[] key;
        public string key_str;
        public int head_len;

        public ServerInfo(string host, int port, string param, object data, byte[] iv, string key_str, byte[] key, int head_len, int tcp_mss, int overhead, int buffer_size)
        {
            this.host = host;
            this.port = port;
            this.param = param;
            this.data = data;
            this.Iv = iv;
            this.key = key;
            this.key_str = key_str;
            this.head_len = head_len;
            this.tcp_mss = tcp_mss;
            this.overhead = overhead;
            this.buffer_size = buffer_size;
        }

        public void SetIV(byte[] iv)
        {
            this.Iv = iv;
        }
    }
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
