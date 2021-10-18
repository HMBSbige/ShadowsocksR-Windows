using Shadowsocks.Model;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Shadowsocks.Controller.Service
{
    public class HostDaemon
    {
        private FileSystemWatcher _userRuleWatcher;
        private FileSystemWatcher _chnIpWatcher;

        public event EventHandler UserRuleChanged;
        public event EventHandler ChnIpChanged;

        public HostDaemon()
        {
            WatchChnIp();
            WatchUserRule();
        }

        private void WatchChnIp()
        {
            _chnIpWatcher?.Dispose();
            _chnIpWatcher = new FileSystemWatcher(Directory.GetCurrentDirectory())
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = IPRangeSet.ChnFilename
            };
            _chnIpWatcher.Changed += ChnIpWatcher_Changed;
            _chnIpWatcher.Created += ChnIpWatcher_Changed;
            _chnIpWatcher.Deleted += ChnIpWatcher_Changed;
            _chnIpWatcher.Renamed += ChnIpWatcher_Changed;
            _chnIpWatcher.EnableRaisingEvents = true;
        }

        private void WatchUserRule()
        {
            _userRuleWatcher?.Dispose();
            _userRuleWatcher = new FileSystemWatcher(Directory.GetCurrentDirectory())
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = HostMap.UserRule
            };
            _userRuleWatcher.Changed += UserRuleWatcher_Changed;
            _userRuleWatcher.Created += UserRuleWatcher_Changed;
            _userRuleWatcher.Deleted += UserRuleWatcher_Changed;
            _userRuleWatcher.Renamed += UserRuleWatcher_Changed;
            _userRuleWatcher.EnableRaisingEvents = true;
        }

        private void UserRuleWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (UserRuleChanged == null)
            {
                return;
            }

            try
            {
                ((FileSystemWatcher)sender).EnableRaisingEvents = false;
                Logging.Info($@"Detected: user rule file '{e.Name}' was {e.ChangeType.ToString().ToLower()}.");
                Task.Delay(10).ContinueWith(task => UserRuleChanged(this, EventArgs.Empty));
            }
            finally
            {
                ((FileSystemWatcher)sender).EnableRaisingEvents = true;
            }
        }

        private void ChnIpWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (ChnIpChanged == null)
            {
                return;
            }

            try
            {
                ((FileSystemWatcher)sender).EnableRaisingEvents = false;
                Logging.Info($@"Detected: '{e.Name}' was {e.ChangeType.ToString().ToLower()}.");
                Task.Delay(10).ContinueWith(task => ChnIpChanged(this, EventArgs.Empty));
            }
            finally
            {
                ((FileSystemWatcher)sender).EnableRaisingEvents = true;
            }
        }
    }
}
