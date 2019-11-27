using Newtonsoft.Json;
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
        private string tag;
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
                if (SetField(ref url, value))
                {
                    SubscribeChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        [JsonIgnore]
        public string OriginTag => tag;

        [JsonIgnore]
        public string UrlMd5 => BitConverter.ToString(MbedTLS.MD5(Encoding.UTF8.GetBytes(Url))).Replace(@"-", string.Empty);

        public string Tag
        {
            get => string.IsNullOrWhiteSpace(tag) ? UrlMd5 : tag;
            set
            {
                if (UrlMd5 == value)
                {
                    value = string.Empty;
                }
                if (SetField(ref tag, value))
                {
                    SubscribeChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        public ulong LastUpdateTime
        {
            get => lastUpdateTime;
            set
            {
                if (SetField(ref lastUpdateTime, value))
                {
                    SubscribeChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        public bool AutoCheckUpdate
        {
            get => autoCheckUpdate;
            set
            {
                if (SetField(ref autoCheckUpdate, value))
                {
                    SubscribeChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        public event EventHandler SubscribeChanged;
    }
}