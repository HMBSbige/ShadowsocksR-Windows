﻿
using System;
using System.Collections.Generic;

namespace Shadowsocks.Obfs
{
    public abstract class ObfsBase: IObfs
    {
        protected ObfsBase(string method)
        {
            Method = method;
        }

        protected string Method;
        protected ServerInfo Server;
        protected long SentLength;

        public abstract Dictionary<string, int[]> GetObfs();

        public string Name()
        {
            return Method;
        }

        public virtual bool isKeepAlive()
        {
            return false;
        }

        public virtual bool isAlwaysSendback()
        {
            return false;
        }


        public virtual byte[] ClientPreEncrypt(byte[] plaindata, int datalength, out int outlength)
        {
            outlength = datalength;
            return plaindata;
        }
        public abstract byte[] ClientEncode(byte[] encryptdata, int datalength, out int outlength);
        public abstract byte[] ClientDecode(byte[] encryptdata, int datalength, out int outlength, out bool needsendback);
        public virtual byte[] ClientPostDecrypt(byte[] plaindata, int datalength, out int outlength)
        {
            outlength = datalength;
            return plaindata;
        }
        public virtual byte[] ClientUdpPreEncrypt(byte[] plaindata, int datalength, out int outlength)
        {
            outlength = datalength;
            return plaindata;
        }
        public virtual byte[] ClientUdpPostDecrypt(byte[] plaindata, int datalength, out int outlength)
        {
            outlength = datalength;
            return plaindata;
        }

        public virtual object InitData()
        {
            return null;
        }
        public virtual void SetServerInfo(ServerInfo serverInfo)
        {
            Server = serverInfo;
        }
        public virtual void SetServerInfoIV(byte[] iv)
        {
            Server.SetIV(iv);
        }
        public static int GetHeadSize(byte[] plaindata, int defaultValue)
        {
            if (plaindata == null || plaindata.Length < 2)
                return defaultValue;
            int head_type = plaindata[0] & 0x7;
            if (head_type == 1)
                return 7;
            if (head_type == 4)
                return 19;
            if (head_type == 3)
                return 4 + plaindata[1];
            if (head_type == 2)
                return 4 + plaindata[1];
            return defaultValue;
        }
        public long GetSentLength()
        {
            return SentLength;
        }
        public virtual int GetOverhead()
        {
            return 0;
        }

        public int GetTcpMSS()
        {
            return Server.tcp_mss;
        }


        #region IDisposable
        protected bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                Disposing();
            }
        }

        protected virtual void Disposing()
        {

        }
        #endregion

    }
}
