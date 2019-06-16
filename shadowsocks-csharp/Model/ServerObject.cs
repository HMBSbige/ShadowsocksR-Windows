using Shadowsocks.ViewModel;
using System;
using System.Text;

namespace Shadowsocks.Model
{
    public class ServerObject : ViewModelBase, ICloneable
    {
        private string id;
        private string server;
        private ushort server_port;
        private ushort server_udp_port;
        private string password;
        private string method;
        private string protocol;
        private string protocolparam;
        private string obfs;
        private string obfsparam;
        private string remarks;
        private string group;
        private bool udp_over_tcp;

        private object protocoldata;
        private object obfsdata;
        private bool enable;

        public ServerObject()
        {
            server = @"server host";
            server_port = 8388;
            method = @"aes-256-cfb";
            protocol = @"origin";
            protocolparam = @"";
            obfs = @"plain";
            obfsparam = @"";
            password = @"0";
            remarks = @""; //remarks_base64 = "";
            group = @"FreeSSR-public";
            udp_over_tcp = false;
            enable = true;
            var randId = new byte[16];
            Util.Utils.RandBytes(randId, randId.Length);
            id = BitConverter.ToString(randId).Replace("-", "");
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

        public string ServerName
        {
            get => server;
            set
            {
                if (server != value)
                {
                    server = value;
                    OnPropertyChanged();
                }
            }
        }

        public ushort ServerPort
        {
            get => server_port;
            set
            {
                if (server_port != value)
                {
                    server_port = value;
                    OnPropertyChanged();
                }
            }
        }

        public ushort ServerUdpPort
        {
            get => server_udp_port;
            set
            {
                if (server_udp_port != value)
                {
                    server_udp_port = value;
                    OnPropertyChanged();
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

        public string ObfsName
        {
            get => string.IsNullOrWhiteSpace(obfs) ? @"plain" : obfs;
            set
            {
                if (obfs != value)
                {
                    obfs = value;
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

        public string Remarks
        {
            get => remarks;
            set
            {
                if (remarks != value)
                {
                    remarks = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RemarkName));
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

        public string GroupName => string.IsNullOrEmpty(Group) ? @"(empty group)" : Group;
        public string RemarkName => string.IsNullOrEmpty(Remarks) ? @"(empty remark)" : Remarks;

        public bool UdpOverTcp
        {
            get => udp_over_tcp;
            set
            {
                if (udp_over_tcp != value)
                {
                    udp_over_tcp = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowAdvSetting => UdpOverTcp || ServerUdpPort != 0;

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

        public string SsLink => GetSsLink();
        public string SsrLink => GetSsrLink();

        #region Method

        public static ServerObject GetDefaultServer()
        {
            return new ServerObject();
        }

        public static ServerObject CopyFromServer(Server server)
        {
            return new ServerObject
            {
                Id = server.id,
                ServerName = server.server,
                ServerPort = server.server_port,
                ServerUdpPort = server.server_udp_port,
                Password = server.password,
                Method = server.method,
                Protocol = server.protocol,
                ProtocolParam = server.protocolparam,
                ObfsName = server.obfs,
                ObfsParam = server.obfsparam,
                Remarks = server.remarks,
                Group = server.group,
                UdpOverTcp = server.udp_over_tcp
            };
        }

        public object Clone()
        {
            throw new NotImplementedException();
        }

        public static ServerObject Clone(ServerObject serverObject)
        {
            return new ServerObject
            {
                ServerName = serverObject.ServerName,
                ServerPort = serverObject.ServerPort,
                ServerUdpPort = serverObject.ServerUdpPort,
                Password = serverObject.Password,
                Method = serverObject.Method,
                Protocol = serverObject.Protocol,
                ProtocolParam = serverObject.ProtocolParam,
                ObfsName = serverObject.ObfsName,
                ObfsParam = serverObject.ObfsParam,
                Remarks = serverObject.Remarks,
                Group = serverObject.Group,
                UdpOverTcp = serverObject.UdpOverTcp
            };
        }

        private string GetSsLink()
        {
            var parts = $@"{method}:{password}@{server}:{server_port}";
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(parts)).Replace(@"=", string.Empty);
            return $@"ss://{base64}";
        }

        private string GetSsrLink()
        {
            var mainPart = $@"{server}:{server_port}:{protocol}:{method}:{obfs}:{Util.Base64.EncodeUrlSafeBase64(password)}";
            var paramStr = $@"obfsparam={Util.Base64.EncodeUrlSafeBase64(obfsparam ?? string.Empty)}";
            if (!string.IsNullOrEmpty(protocolparam))
            {
                paramStr += $@"&protoparam={Util.Base64.EncodeUrlSafeBase64(protocolparam)}";
            }

            if (!string.IsNullOrEmpty(remarks))
            {
                paramStr += $@"&remarks={Util.Base64.EncodeUrlSafeBase64(remarks)}";
            }

            if (!string.IsNullOrEmpty(group))
            {
                paramStr += $@"&group={Util.Base64.EncodeUrlSafeBase64(@group)}";
            }

            if (udp_over_tcp)
            {
                paramStr += @"&uot=1";
            }

            if (server_udp_port > 0)
            {
                paramStr += $@"&udpport={server_udp_port}";
            }

            var base64 = Util.Base64.EncodeUrlSafeBase64($@"{mainPart}/?{paramStr}");
            return $@"ssr://{base64}";
        }

        protected override void OnPropertyChanged(string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            base.OnPropertyChanged(nameof(SsLink));
            base.OnPropertyChanged(nameof(SsrLink));
        }

        #endregion
    }
}
