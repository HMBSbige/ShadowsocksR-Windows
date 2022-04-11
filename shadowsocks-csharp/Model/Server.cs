using Shadowsocks.Model.Transfer;
using Shadowsocks.Util;
using Shadowsocks.ViewModel;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Shadowsocks.Model
{
    [Serializable]
    public class Server : ViewModelBase, ICloneable
    {
        #region private

        private string _id;
        private string _server;
        private ushort _serverPort;
        private ushort _serverUdpPort;
        private string _password;
        private string _method;
        private string _protocol;
        private string _protocolParam;
        private string _obfs;
        private string _obfsParam;
        private string _remarksBase64;
        private string _group;
        private string _subTag;
        private bool _enable;
        private bool _udpOverTcp;

        #endregion

        #region Public

        public string Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        public string server
        {
            get => _server;
            set
            {
                if (SetField(ref _server, value))
                {
                    OnPropertyChanged(nameof(FriendlyName));
                }
            }
        }

        public ushort Server_Port
        {
            get => _serverPort;
            set
            {
                if (SetField(ref _serverPort, value))
                {
                    OnPropertyChanged(nameof(FriendlyName));
                }
            }
        }

        public ushort Server_Udp_Port
        {
            get => _serverUdpPort;
            set
            {
                if (SetField(ref _serverUdpPort, value))
                {
                    OnPropertyChanged(nameof(FriendlyName));
                }
            }
        }

        public string Password
        {
            get => _password;
            set => SetField(ref _password, value);
        }

        public string Method
        {
            get => string.IsNullOrWhiteSpace(_method) ? @"aes-256-cfb" : _method;
            set => SetField(ref _method, value);
        }

        public string Protocol
        {
            get => string.IsNullOrWhiteSpace(_protocol) ? @"origin" : _protocol;
            set => SetField(ref _protocol, value);
        }

        public string ProtocolParam
        {
            get => _protocolParam ?? string.Empty;
            set => SetField(ref _protocolParam, value);
        }

        public string obfs
        {
            get => string.IsNullOrWhiteSpace(_obfs) ? @"plain" : _obfs;
            set => SetField(ref _obfs, value);
        }

        public string ObfsParam
        {
            get => _obfsParam ?? string.Empty;
            set => SetField(ref _obfsParam, value);
        }

        public string Remarks_Base64
        {
            get => _remarksBase64;
            set
            {
                if (SetField(ref _remarksBase64, value))
                {
                    OnPropertyChanged(nameof(Remarks));
                    OnPropertyChanged(nameof(FriendlyName));
                }
            }
        }

        public string Group
        {
            get => _group;
            set
            {
                if (SetField(ref _group, value))
                {
                    OnPropertyChanged(nameof(GroupName));
                }
            }
        }

        public string SubTag
        {
            get => _subTag;
            set => SetField(ref _subTag, value);
        }

        public bool Enable
        {
            get => _enable;
            set => SetField(ref _enable, value);
        }

        public bool UdpOverTcp
        {
            get => _udpOverTcp;
            set
            {
                if (SetField(ref _udpOverTcp, value))
                {
                    OnPropertyChanged(nameof(ShowAdvSetting));
                }
            }
        }

        #endregion

        #region NotConfig

        private int _index;
        private bool _isSelected;
        private ServerSpeedLog _serverSpeedLog;

        [JsonIgnore]
        public int Index
        {
            get => _index;
            set => SetField(ref _index, value);
        }

        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
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
                var newValue = Base64.EncodeUrlSafeBase64(value);
                if (newValue != Remarks_Base64)
                {
                    Remarks_Base64 = newValue;
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
        public string SsLink
        {
            get
            {
                var parts = $@"{Method}:{Password}@{server}:{Server_Port}";
                var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(parts)).Replace(@"=", string.Empty);
                return $@"ss://{base64}";
            }
        }

        [JsonIgnore]
        public string SsrLink
        {
            get
            {
                var mainPart = $@"{server}:{Server_Port}:{Protocol}:{Method}:{obfs}:{Base64.EncodeUrlSafeBase64(Password)}";
                var paramStr = $@"obfsparam={Base64.EncodeUrlSafeBase64(ObfsParam)}";
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
        }

        [JsonIgnore]
        public bool ShowAdvSetting => UdpOverTcp || Server_Udp_Port != 0;

        [JsonIgnore]
        public object ProtocolData { get; set; }

        [JsonIgnore]
        public object ObfsData { get; set; }

        [JsonIgnore]
        public ServerSpeedLog SpeedLog
        {
            get => _serverSpeedLog;
            set => SetField(ref _serverSpeedLog, value);
        }

        [JsonIgnore]
        public Connections Connections { get; private set; } = new();

        [JsonIgnore]
        public DnsBuffer DnsBuffer { get; private set; } = new();

        [JsonIgnore]
        public static Server ForwardServer { get; } = new();

        #endregion

        public void CopyServer(Server oldServer)
        {
            ProtocolData = oldServer.ProtocolData;
            ObfsData = oldServer.ObfsData;
            SpeedLog = oldServer.SpeedLog;
            DnsBuffer = oldServer.DnsBuffer;
            Connections = oldServer.Connections;
            Enable = oldServer.Enable;
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
                ProtocolData = ProtocolData,
                ObfsData = ObfsData
            };
        }

        public static Server Clone(Server serverObject)
        {
            return new()
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
            Id = Guid.NewGuid().ToString(@"N");
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
            var ssr = Regex.Match(ssrUrl, "ssr://([A-Za-z0-9+/=_-]+)", RegexOptions.IgnoreCase);
            if (!ssr.Success)
            {
                throw new FormatException();
            }

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
            {
                throw new FormatException();
            }

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
            Regex UrlFinder = new("^(?i)ss://([A-Za-z0-9+-/=_]+)(#(.+))?", RegexOptions.IgnoreCase),
                DetailsParser = new("^((?<method>.+):(?<password>.*)@(?<hostname>.+?)" +
                                    ":(?<port>\\d+?))$", RegexOptions.IgnoreCase);

            var match = UrlFinder.Match(ssUrl);
            if (!match.Success)
            {
                throw new FormatException();
            }

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

        public event EventHandler ServerChanged;

        protected override bool SetField<T>(ref T field, T value, string propertyName = @"")
        {
            if (base.SetField(ref field, value, propertyName))
            {
                OnPropertyChanged(nameof(SsLink));
                OnPropertyChanged(nameof(SsrLink));
                ServerChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }
    }
}
