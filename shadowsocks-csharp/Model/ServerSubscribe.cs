#if IsDotNetCore
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif
using Shadowsocks.Controller.HttpRequest;
using Shadowsocks.Encryption;
using Shadowsocks.Enums;
using Shadowsocks.ViewModel;
using System;
using System.Text;

namespace Shadowsocks.Model
{
    [Serializable]
    public class ServerSubscribe : ViewModelBase
    {
        private string _url;
        private string _tag;
        private ulong _lastUpdateTime;
        private bool _autoCheckUpdate;
        private HttpRequestProxyType _proxyType;

        public ServerSubscribe()
        {
            _url = UpdateNode.DefaultUpdateUrl;
            _autoCheckUpdate = true;
            _proxyType = HttpRequestProxyType.Auto;
        }

        public string Url
        {
            get => _url;
            set
            {
                if (SetField(ref _url, value))
                {
                    SubscribeChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        [JsonIgnore]
        public string OriginTag => _tag;

        [JsonIgnore]
        public string UrlMd5 => BitConverter.ToString(MbedTLS.MD5(Encoding.UTF8.GetBytes(Url))).Replace(@"-", string.Empty);

        public string Tag
        {
            get => string.IsNullOrWhiteSpace(_tag) ? UrlMd5 : _tag;
            set
            {
                if (UrlMd5 == value)
                {
                    value = string.Empty;
                }
                if (SetField(ref _tag, value))
                {
                    SubscribeChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        public ulong LastUpdateTime
        {
            get => _lastUpdateTime;
            set
            {
                if (SetField(ref _lastUpdateTime, value))
                {
                    SubscribeChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        public bool AutoCheckUpdate
        {
            get => _autoCheckUpdate;
            set
            {
                if (SetField(ref _autoCheckUpdate, value))
                {
                    SubscribeChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        public HttpRequestProxyType ProxyType
        {
            get => _proxyType;
            set
            {
                if (SetField(ref _proxyType, value))
                {
                    SubscribeChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        public event EventHandler SubscribeChanged;
    }
}