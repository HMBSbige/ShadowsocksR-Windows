﻿using Newtonsoft.Json;
using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Encryption;
using Shadowsocks.ViewModel;
using System;
using System.Text;

namespace Shadowsocks.Model
{
    [Serializable]
    public class ServerSubscribe : ViewModelBase
    {
        private string url;
        private string group;
        private ulong lastUpdateTime;
        private bool autoCheckUpdate;

        public ServerSubscribe()
        {
            url = UpdateNode.DefaultUpdateUrl;
            autoCheckUpdate = true;
        }

        public string Url
        {
            get => url;
            set
            {
                if (url != value)
                {
                    url = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public string OriginGroup => group;

        [JsonIgnore]
        public string UrlMd5 => BitConverter.ToString(MbedTLS.MD5(Encoding.UTF8.GetBytes(Url))).Replace(@"-", string.Empty);

        public string Group
        {
            get => string.IsNullOrWhiteSpace(group) ? UrlMd5 : group;
            set
            {
                if (UrlMd5 == value)
                {
                    group = string.Empty;
                    OnPropertyChanged();
                }
                else if (group != value)
                {
                    group = value;
                    OnPropertyChanged();
                }
            }
        }

        public ulong LastUpdateTime
        {
            get => lastUpdateTime;
            set
            {
                if (lastUpdateTime != value)
                {
                    lastUpdateTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoCheckUpdate
        {
            get => autoCheckUpdate;
            set
            {
                if (autoCheckUpdate != value)
                {
                    autoCheckUpdate = value;
                    OnPropertyChanged();
                }
            }
        }

        public event EventHandler SubscribeChanged;

        protected override void OnPropertyChanged(string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            SubscribeChanged?.Invoke(this, new EventArgs());
        }
    }
}