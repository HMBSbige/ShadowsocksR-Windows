using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Shadowsocks.Encryption;
using Shadowsocks.Util;

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
                var config = new ServerTransferTotal();
                try
                {
                    if (GlobalConfiguration.config_password.Length > 0)
                    {
                        using var encryptor = EncryptorFactory.GetEncryptor(@"aes-256-cfb", GlobalConfiguration.config_password);
                        config_str = Encoding.UTF8.GetString(Utils.DecryptLargeBase64StringToBytes(encryptor, config_str));
                    }
                }
                catch
                {
                    // ignored
                }

                config.servers = JsonConvert.DeserializeObject<Dictionary<string, ServerTrans>>(config_str);
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
                if (GlobalConfiguration.config_password.Length > 0)
                {
                    using var encryptor = EncryptorFactory.GetEncryptor(@"aes-256-cfb", GlobalConfiguration.config_password);
                    var cfgData = Encoding.UTF8.GetBytes(jsonString);
                    jsonString = Utils.EncryptLargeBytesToBase64String(encryptor, cfgData);
                }
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