using Shadowsocks.Properties;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Shadowsocks.Controller.Service
{
    /// <summary>
    /// Processing the PAC file content
    /// </summary>
    public class PACDaemon
    {
        public const string PAC_FILE = @"pac.txt";
        public const string USER_RULE_FILE = @"user-rule.txt";
        public const string USER_ABP_FILE = @"abp.txt";

        FileSystemWatcher PACFileWatcher;
        FileSystemWatcher UserRuleFileWatcher;

        public event EventHandler PACFileChanged;
        public event EventHandler UserRuleFileChanged;

        public PACDaemon()
        {
            TouchPACFile();
            TouchUserRuleFile();

            WatchPacFile();
            WatchUserRuleFile();
        }

        public string TouchPACFile()
        {
            if (!File.Exists(PAC_FILE))
            {
                File.WriteAllText(PAC_FILE, Resources.proxy_pac);
            }
            return PAC_FILE;
        }

        internal string TouchUserRuleFile()
        {
            if (!File.Exists(USER_RULE_FILE))
            {
                File.WriteAllText(USER_RULE_FILE, Resources.user_rule);
            }
            return USER_RULE_FILE;
        }

        internal string GetPACContent()
        {
            if (File.Exists(PAC_FILE))
            {
                return File.ReadAllText(PAC_FILE, Encoding.UTF8);
            }
            return Resources.proxy_pac;
        }

        private void WatchPacFile()
        {
            PACFileWatcher?.Dispose();
            PACFileWatcher = new FileSystemWatcher(Directory.GetCurrentDirectory())
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = PAC_FILE
            };
            PACFileWatcher.Changed += PACFileWatcher_Changed;
            PACFileWatcher.Created += PACFileWatcher_Changed;
            PACFileWatcher.Deleted += PACFileWatcher_Changed;
            PACFileWatcher.Renamed += PACFileWatcher_Changed;
            PACFileWatcher.EnableRaisingEvents = true;
        }

        private void WatchUserRuleFile()
        {
            UserRuleFileWatcher?.Dispose();
            UserRuleFileWatcher = new FileSystemWatcher(Directory.GetCurrentDirectory())
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = USER_RULE_FILE
            };
            UserRuleFileWatcher.Changed += UserRuleFileWatcher_Changed;
            UserRuleFileWatcher.Created += UserRuleFileWatcher_Changed;
            UserRuleFileWatcher.Deleted += UserRuleFileWatcher_Changed;
            UserRuleFileWatcher.Renamed += UserRuleFileWatcher_Changed;
            UserRuleFileWatcher.EnableRaisingEvents = true;
        }

        #region FileSystemWatcher.OnChanged()
        // FileSystemWatcher Changed event is raised twice
        // http://stackoverflow.com/questions/1764809/filesystemwatcher-changed-event-is-raised-twice
        // Add a short delay to avoid raise event twice in a short period
        private void PACFileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (PACFileChanged == null)
            {
                return;
            }

            try
            {
                ((FileSystemWatcher)sender).EnableRaisingEvents = false;
                Logging.Info($@"Detected: PAC file '{e.Name}' was {e.ChangeType.ToString().ToLower()}.");
                Task.Delay(10).ContinueWith(task => { PACFileChanged(this, EventArgs.Empty); });
            }
            finally
            {
                ((FileSystemWatcher)sender).EnableRaisingEvents = true;
            }
        }

        private void UserRuleFileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (UserRuleFileChanged == null)
            {
                return;
            }

            try
            {
                ((FileSystemWatcher)sender).EnableRaisingEvents = false;
                Logging.Info($@"Detected: User Rule file '{e.Name}' was {e.ChangeType.ToString().ToLower()}.");
                Task.Delay(10).ContinueWith(task => { UserRuleFileChanged(this, EventArgs.Empty); });
            }
            finally
            {
                ((FileSystemWatcher)sender).EnableRaisingEvents = true;
            }
        }
        #endregion
    }
}
