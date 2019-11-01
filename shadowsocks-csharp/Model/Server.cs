using Newtonsoft.Json;
using Shadowsocks.Model.Transfer;
using Shadowsocks.Util;
using Shadowsocks.ViewModel;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Shadowsocks.Model
{
    [Serializable]
    public class Server : ViewModelBase, ICloneable
    {
        #region private

        private string id;
        private string _server;
        private ushort server_port;
        private ushort server_udp_port;
        private string password;
        private string method;
        private string protocol;
        private string protocolparam;
        private string _obfs;
        private string obfsparam;
        private string remarks_base64;
        private string group;
        private string subTag;
        private bool enable;
        private bool udp_over_tcp;

        private object protocoldata;
        private object obfsdata;

        private ServerSpeedLog serverSpeedLog;
        private DnsBuffer dnsBuffer = new DnsBuffer();
        [JsonIgnore]
        public Connections Connections { get; private set; } = new Connections();
        private static readonly Server ForwardServer = new Server();

        private int _index;
        private bool _isSelected;

        #endregion

        #region Public

        [JsonIgnore]
        public int Index
        {
            get => _index;
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public string GroupName => string.IsNullOrEmpty(Group) ? I18NUtil.GetAppStringValue(@"EmptyGroup") : Group;

        [JsonIgnore]
        public string Remarks
        {
            get
            {
                if (Remarks_Base64.Length == 0)
                {
                    return string.Empty;
                }

                try
                {
                    return Base64.DecodeUrlSafeBase64(Remarks_Base64);
                }
                catch (FormatException)
                {
                    var old = Remarks_Base64;
                    Remarks = Remarks_Base64;
                    return old;
                }
            }
            set
            {
                var @new = Base64.EncodeUrlSafeBase64(value);
                if (@new != Remarks_Base64)
                {
                    Remarks_Base64 = @new;
                }
            }
        }

        [JsonIgnore]
        public string FriendlyName
        {
            get
            {
                if (string.IsNullOrEmpty(server))
                {
                    return I18NUtil.GetAppStringValue(@"NewServer");
                }

                if (string.IsNullOrEmpty(Remarks))
                {
                    if (server.IndexOf(':') >= 0)
                    {
                        return $@"[{server}]:{Server_Port}";
                    }

                    return $@"{server}:{Server_Port}";
                }

                return $@"{Remarks}";
            }
        }

        [JsonIgnore]
        public string SsLink => GetSsLink();

        [JsonIgnore]
        public string SsrLink => GetSsrLink();

        [JsonIgnore]
        public bool ShowAdvSetting => UdpOverTcp || Server_Udp_Port != 0;

        [JsonIgnore]
        public object Protocoldata
        {
            get => protocoldata;
            set
            {
                if (protocoldata != value)
                {
                    protocoldata = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public object Obfsdata
        {
            get => obfsdata;
            set
            {
                if (obfsdata != value)
                {
                    obfsdata = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public ServerSpeedLog SpeedLog
        {
            get => serverSpeedLog;
            set
            {
                if (serverSpeedLog != value)
                {
                    serverSpeedLog = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Id
        {
            get => id;
            set
            {
                if (id != value)
                {
                    id = value;
                    OnPropertyChanged();
                }
            }
        }

        public string server
        {
            get => _server;
            set
            {
                if (_server != value)
                {
                    _server = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FriendlyName));
                }
            }
        }

        public ushort Server_Port
        {
            get => server_port;
            set
            {
                if (server_port != value)
                {
                    server_port = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FriendlyName));
                }
            }
        }

        public ushort Server_Udp_Port
        {
            get => server_udp_port;
            set
            {
                if (server_udp_port != value)
                {
                    server_udp_port = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowAdvSetting));
                }
            }
        }

        public string Password
        {
            get => password;
            set
            {
                if (password != value)
                {
                    password = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Method
        {
            get => string.IsNullOrWhiteSpace(method) ? @"aes-256-cfb" : method;
            set
            {
                if (method != value)
                {
                    method = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Protocol
        {
            get => string.IsNullOrWhiteSpace(protocol) ? @"origin" : protocol;
            set
            {
                if (protocol != value)
                {
                    protocol = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ProtocolParam
        {
            get => protocolparam ?? string.Empty;
            set
            {
                if (protocolparam != value)
                {
                    protocolparam = value;
                    OnPropertyChanged();
                }
            }
        }

        public string obfs
        {
            get => string.IsNullOrWhiteSpace(_obfs) ? @"plain" : _obfs;
            set
            {
                if (_obfs != value)
                {
                    _obfs = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ObfsParam
        {
            get => obfsparam ?? string.Empty;
            set
            {
                if (obfsparam != value)
                {
                    obfsparam = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Group
        {
            get => group;
            set
            {
                if (group != value)
                {
                    group = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GroupName));
                }
            }
        }

        public string SubTag
        {
            get => subTag;
            set
            {
                if (subTag != value)
                {
                    subTag = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Remarks_Base64
        {
            get => remarks_base64;
            set
            {
                if (remarks_base64 != value)
                {
                    remarks_base64 = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Remarks));
                    OnPropertyChanged(nameof(FriendlyName));
                }
            }
        }

        public bool Enable
        {
            get => enable;
            set
            {
                if (enable != value)
                {
                    enable = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool UdpOverTcp
        {
            get => udp_over_tcp;
            set
            {
                if (udp_over_tcp != value)
                {
                    udp_over_tcp = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowAdvSetting));
                }
            }
        }

        #endregion

        public static Server GetDefaultServer()
        {
            return new Server();
        }

        public void CopyServer(Server oldServer)
        {
            Protocoldata = oldServer.Protocoldata;
            Obfsdata = oldServer.Obfsdata;
            SpeedLog = oldServer.SpeedLog;
            dnsBuffer = oldServer.dnsBuffer;
            Connections = oldServer.Connections;
            Enable = oldServer.Enable;
        }

        public static Server GetForwardServerRef()
        {
            return ForwardServer;
        }

        public DnsBuffer DnsBuffer()
        {
            return dnsBuffer;
        }

        public object Clone()
        {
            return new Server
            {
                server = server,
                Server_Port = Server_Port,
                Password = Password,
                Method = Method,
                Protocol = Protocol,
                obfs = obfs,
                ObfsParam = ObfsParam,
                Remarks_Base64 = Remarks_Base64,
                Group = Group,
                Enable = Enable,
                UdpOverTcp = UdpOverTcp,

                Id = Id,
                Protocoldata = Protocoldata,
                Obfsdata = Obfsdata
            };
        }

        public static Server Clone(Server serverObject)
        {
            return new Server
            {
                server = serverObject.server,
                Server_Port = serverObject.Server_Port,
                Server_Udp_Port = serverObject.Server_Udp_Port,
                Password = serverObject.Password,
                Method = serverObject.Method,
                Protocol = serverObject.Protocol,
                ProtocolParam = serverObject.ProtocolParam,
                obfs = serverObject.obfs,
                ObfsParam = serverObject.ObfsParam,
                Remarks = serverObject.Remarks,
                Group = serverObject.Group,
                UdpOverTcp = serverObject.UdpOverTcp
            };
        }

        public Server()
        {
            server = @"server host";
            Server_Port = 8388;
            Method = @"aes-256-cfb";
            Protocol = @"origin";
            ProtocolParam = @"";
            obfs = @"plain";
            ObfsParam = @"";
            Password = @"0";
            Remarks_Base64 = @"";
            Group = @"Default Group";
            SubTag = @"";
            UdpOverTcp = false;
            Enable = true;
            Id = Rng.RandId();
            SpeedLog = new ServerSpeedLog();
            Index = 0;
            IsSelected = false;
        }

        public Server(string ssUrl, string forceGroup) : this()
        {
            if (ssUrl.StartsWith(@"ss://", StringComparison.OrdinalIgnoreCase))
            {
                ServerFromSs(ssUrl, forceGroup);
            }
            else if (ssUrl.StartsWith(@"ssr://", StringComparison.OrdinalIgnoreCase))
            {
                ServerFromSsr(ssUrl, forceGroup);
            }
            else
            {
                throw new FormatException();
            }
        }

        private static Dictionary<string, string> ParseParam(string paramStr)
        {
            var paramsDict = new Dictionary<string, string>();
            var obfsParams = paramStr.Split('&');
            foreach (var p in obfsParams)
            {
                if (p.IndexOf('=') > 0)
                {
                    var index = p.IndexOf('=');
                    var key = p.Substring(0, index);
                    var val = p.Substring(index + 1);
                    paramsDict[key] = val;
                }
            }
            return paramsDict;
        }

        public void ServerFromSsr(string ssrUrl, string forceGroup)
        {
            // ssr://host:port:protocol:method:obfs:base64pass/?obfsparam=base64&remarks=base64&group=base64&udpport=0&uot=1
            var ssr = Regex.Match(ssrUrl, "ssr://([A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
            if (!ssr.Success)
                throw new FormatException();

            var data = Base64.DecodeUrlSafeBase64(ssr.Groups[1].Value);
            var params_dict = new Dictionary<string, string>();

            var param_start_pos = data.IndexOf("?", StringComparison.Ordinal);
            if (param_start_pos > 0)
            {
                params_dict = ParseParam(data.Substring(param_start_pos + 1));
                data = data.Substring(0, param_start_pos);
            }
            if (data.IndexOf("/", StringComparison.Ordinal) >= 0)
            {
                data = data.Substring(0, data.LastIndexOf("/", StringComparison.Ordinal));
            }

            var UrlFinder = new Regex("^(.+):([^:]+):([^:]*):([^:]+):([^:]*):([^:]+)");
            var match = UrlFinder.Match(data);

            if (match == null || !match.Success)
                throw new FormatException();

            server = match.Groups[1].Value;
            Server_Port = ushort.Parse(match.Groups[2].Value);
            Protocol = match.Groups[3].Value.Length == 0 ? "origin" : match.Groups[3].Value;
            Protocol = Protocol.Replace("_compatible", "");
            Method = match.Groups[4].Value;
            obfs = match.Groups[5].Value.Length == 0 ? "plain" : match.Groups[5].Value;
            obfs = obfs.Replace("_compatible", "");
            Password = Base64.DecodeUrlSafeBase64(match.Groups[6].Value);

            if (params_dict.ContainsKey("protoparam"))
            {
                ProtocolParam = Base64.DecodeUrlSafeBase64(params_dict["protoparam"]);
            }
            if (params_dict.ContainsKey("obfsparam"))
            {
                ObfsParam = Base64.DecodeUrlSafeBase64(params_dict["obfsparam"]);
            }
            if (params_dict.ContainsKey("remarks"))
            {
                Remarks = Base64.DecodeUrlSafeBase64(params_dict["remarks"]);
            }
            Group = params_dict.ContainsKey("group") ? Base64.DecodeUrlSafeBase64(params_dict["group"]) : string.Empty;

            if (params_dict.ContainsKey("uot"))
            {
                UdpOverTcp = int.Parse(params_dict["uot"]) != 0;
            }
            if (params_dict.ContainsKey("udpport"))
            {
                Server_Udp_Port = ushort.Parse(params_dict["udpport"]);
            }
            if (!string.IsNullOrEmpty(forceGroup))
            {
                SubTag = forceGroup;
            }
        }

        private void ServerFromSs(string ssUrl, string forceGroup)
        {
            Regex UrlFinder = new Regex("^(?i)ss://([A-Za-z0-9+-/=_]+)(#(.+))?", RegexOptions.IgnoreCase),
                DetailsParser = new Regex("^((?<method>.+):(?<password>.*)@(?<hostname>.+?)" +
                                      ":(?<port>\\d+?))$", RegexOptions.IgnoreCase);

            var match = UrlFinder.Match(ssUrl);
            if (!match.Success)
                throw new FormatException();

            var base64 = match.Groups[1].Value;
            match = DetailsParser.Match(Encoding.UTF8.GetString(Convert.FromBase64String(
                base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='))));
            Protocol = "origin";
            Method = match.Groups["method"].Value;
            Password = match.Groups["password"].Value;
            server = match.Groups["hostname"].Value;
            Server_Port = ushort.Parse(match.Groups["port"].Value);
            SubTag = !string.IsNullOrEmpty(forceGroup) ? forceGroup : string.Empty;
        }

        public bool IsMatchServer(Server serverObject)
        {
            return server == serverObject.server
                   && Server_Port == serverObject.Server_Port
                   && Server_Udp_Port == serverObject.Server_Udp_Port
                   && Method == serverObject.Method
                   && Protocol == serverObject.Protocol
                   && ProtocolParam == serverObject.ProtocolParam
                   && obfs == serverObject.obfs
                   && ObfsParam == serverObject.ObfsParam
                   && Password == serverObject.Password
                   && UdpOverTcp == serverObject.UdpOverTcp
                   && Remarks == serverObject.Remarks
                   && Group == serverObject.Group;
        }

        private string GetSsLink()
        {
            var parts = $@"{Method}:{Password}@{server}:{Server_Port}";
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(parts)).Replace(@"=", string.Empty);
            return $@"ss://{base64}";
        }

        private string GetSsrLink()
        {
            var mainPart = $@"{server}:{Server_Port}:{Protocol}:{Method}:{obfs}:{Base64.EncodeUrlSafeBase64(Password)}";
            var paramStr = $@"obfsparam={Base64.EncodeUrlSafeBase64(ObfsParam ?? string.Empty)}";
            if (!string.IsNullOrEmpty(ProtocolParam))
            {
                paramStr += $@"&protoparam={Base64.EncodeUrlSafeBase64(ProtocolParam)}";
            }

            if (!string.IsNullOrEmpty(Remarks))
            {
                paramStr += $@"&remarks={Base64.EncodeUrlSafeBase64(Remarks)}";
            }

            if (!string.IsNullOrEmpty(Group))
            {
                paramStr += $@"&group={Base64.EncodeUrlSafeBase64(Group)}";
            }

            if (UdpOverTcp)
            {
                paramStr += @"&uot=1";
            }

            if (Server_Udp_Port > 0)
            {
                paramStr += $@"&udpport={Server_Udp_Port}";
            }

            var base64 = Base64.EncodeUrlSafeBase64($@"{mainPart}/?{paramStr}");
            return $@"ssr://{base64}";
        }

        public event EventHandler ServerChanged;

        protected override void OnPropertyChanged(string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName == null)
            {
                base.OnPropertyChanged(nameof(SsLink));
                base.OnPropertyChanged(nameof(SsrLink));
                ServerChanged?.Invoke(this, new EventArgs());
            }
        }
    }
}
