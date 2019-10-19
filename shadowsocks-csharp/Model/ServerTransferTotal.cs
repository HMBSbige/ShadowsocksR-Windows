using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Shadowsocks.Model
{
    [Serializable]
    public class ServerTransferTotal
    {
        private const string LOG_FILE = @"transfer_log.json";

        public Dictionary<string, ServerTrans> servers = new Dictionary<string, ServerTrans>();
        private int saveCounter;
        private DateTime saveTime;

        public static ServerTransferTotal Load()
        {
            try
            {
                var config_str = File.ReadAllText(LOG_FILE);
                var config = new ServerTransferTotal
                {
                    servers = JsonConvert.DeserializeObject<Dictionary<string, ServerTrans>>(config_str)
                };

                config.Init();
                return config;
            }
            catch (Exception e)
            {
                if (!(e is FileNotFoundException))
                {
                    Console.WriteLine(e);
                }
                return new ServerTransferTotal();
            }
        }

        private void Init()
        {
            saveCounter = 256;
            saveTime = DateTime.Now;
            if (servers == null)
            {
                servers = new Dictionary<string, ServerTrans>();
            }
        }

        public static void Save(ServerTransferTotal config)
        {
            try
            {
                using var sw = new StreamWriter(File.Open(LOG_FILE, FileMode.Create));
                var jsonString = JsonConvert.SerializeObject(config.servers, Formatting.Indented);
                sw.Write(jsonString);
                sw.Flush();
            }
            catch (IOException e)
            {
                Console.Error.WriteLine(e);
            }
        }

        public void Clear(string server)
        {
            lock (servers)
            {
                if (servers.ContainsKey(server))
                {
                    servers[server].totalUploadBytes = 0;
                    servers[server].totalDownloadBytes = 0;
                }
            }
        }

        public void AddUpload(string server, long size)
        {
            lock (servers)
            {
                if (!servers.ContainsKey(server))
                    servers.Add(server, new ServerTrans());
                servers[server].totalUploadBytes += size;
            }
            if (--saveCounter <= 0)
            {
                saveCounter = 256;
                if ((DateTime.Now - saveTime).TotalMinutes > 10)
                {
                    lock (servers)
                    {
                        Save(this);
                        saveTime = DateTime.Now;
                    }
                }
            }
        }

        public void AddDownload(string server, long size)
        {
            lock (servers)
            {
                if (!servers.ContainsKey(server))
                    servers.Add(server, new ServerTrans());
                servers[server].totalDownloadBytes += size;
            }
            if (--saveCounter <= 0)
            {
                saveCounter = 256;
                if ((DateTime.Now - saveTime).TotalMinutes > 10)
                {
                    lock (servers)
                    {
                        Save(this);
                        saveTime = DateTime.Now;
                    }
                }
            }
        }
    }
}