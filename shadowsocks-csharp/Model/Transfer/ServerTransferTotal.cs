using Shadowsocks.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Shadowsocks.Model.Transfer
{
    [Serializable]
    public class ServerTransferTotal
    {
        private const string LogFile = @"transfer_log.json";

        public Dictionary<string, ServerTrans> Servers = new Dictionary<string, ServerTrans>();
        private int _saveCounter;
        private DateTime _saveTime;

        private static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(10);
        private const int MaxSaveCounter = 256;

        public static ServerTransferTotal Load()
        {
            try
            {
                ServerTransferTotal config;
                if (File.Exists(LogFile))
                {
                    config = new ServerTransferTotal
                    {
                        Servers = JsonUtils.Deserialize<Dictionary<string, ServerTrans>>(File.ReadAllText(LogFile))
                    };
                }
                else
                {
                    config = new ServerTransferTotal();
                }
                config.Init();
                return config;
            }
            catch (FileNotFoundException)
            {
                // ignored
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return new ServerTransferTotal();
        }

        private void Init()
        {
            _saveCounter = MaxSaveCounter;
            _saveTime = DateTime.Now;
            if (Servers == null)
            {
                Servers = new Dictionary<string, ServerTrans>();
            }
        }

        public static void Save(ServerTransferTotal config, List<Server> servers = null)
        {
            try
            {
                if (servers != null)
                {
                    config.Servers = config.Servers
                    .Where(pair => servers.Exists(server => server.Id == pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                }
                var jsonString = JsonUtils.Serialize(config.Servers, true);
                Utils.WriteAllTextAsync(LogFile, jsonString);
            }
            catch (IOException e)
            {
                Console.Error.WriteLine(e);
            }
        }

        public void Clear(string serverId)
        {
            lock (Servers)
            {
                if (Servers.TryGetValue(serverId, out var trans))
                {
                    trans.TotalUploadBytes = 0;
                    trans.TotalDownloadBytes = 0;
                }
            }
        }

        public void AddUpload(string serverId, long size)
        {
            lock (Servers)
            {
                if (Servers.TryGetValue(serverId, out var trans))
                {
                    trans.TotalUploadBytes += size;
                }
                else
                {
                    Servers.Add(serverId, new ServerTrans());
                }
            }
            if (--_saveCounter <= 0)
            {
                _saveCounter = MaxSaveCounter;
                if (DateTime.Now - _saveTime > MinInterval)
                {
                    lock (Servers)
                    {
                        Save(this);
                        _saveTime = DateTime.Now;
                    }
                }
            }
        }

        public void AddDownload(string server, long size)
        {
            lock (Servers)
            {
                if (Servers.TryGetValue(server, out var trans))
                {
                    trans.TotalDownloadBytes += size;
                }
                else
                {
                    Servers.Add(server, new ServerTrans());
                }
            }
            if (--_saveCounter <= 0)
            {
                _saveCounter = MaxSaveCounter;
                if (DateTime.Now - _saveTime > MinInterval)
                {
                    lock (Servers)
                    {
                        Save(this);
                        _saveTime = DateTime.Now;
                    }
                }
            }
        }
    }
}