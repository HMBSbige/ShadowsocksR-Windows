using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Shadowsocks.Enums;
using Shadowsocks.ViewModel;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Shadowsocks.Model;

[Serializable]
public class DnsClient : ViewModelBase
{
    #region private

    private bool _enable;
    private DnsType _dnsType;
    private bool _ipv6First;
    private string _dnsServer;
    private ushort _port;
    private int _timeout;
    private bool _isEDnsEnabled;
    private string _ecsIp;
    private byte _ecsSourceNetmask;
    private byte _ecsScopeNetmask;
    private bool _isTcpEnabled;
    private bool _isUdpEnabled;

    #endregion

    #region public

    public bool Enable
    {
        get => _enable;
        set => SetField(ref _enable, value);
    }

    public DnsType DnsType
    {
        get => _dnsType;
        set => SetField(ref _dnsType, value);
    }

    public bool Ipv6First
    {
        get => _ipv6First;
        set => SetField(ref _ipv6First, value);
    }

    public string DnsServer
    {
        get => IsValidDns(_dnsServer) ? _dnsServer : DefaultDnsServer;
        set
        {
            if (IsValidDns(value))
            {
                SetField(ref _dnsServer, value);
                _ip = null;
            }
        }
    }

    public ushort Port
    {
        get => _port;
        set => SetField(ref _port, value);
    }

    public int Timeout
    {
        get => _timeout;
        set => SetField(ref _timeout, value);
    }

    public bool IsEDnsEnabled
    {
        get => _isEDnsEnabled;
        set => SetField(ref _isEDnsEnabled, value);
    }

    public string EcsIp
    {
        get => IsIp(_ecsIp) ? _ecsIp : DefaultDnsServer;
        set
        {
            if (IsIp(value))
            {
                SetField(ref _ecsIp, value);
            }
        }
    }

    public byte EcsSourceNetmask
    {
        get => _ecsSourceNetmask;
        set => SetField(ref _ecsSourceNetmask, value);
    }

    public byte EcsScopeNetmask
    {
        get => _ecsScopeNetmask;
        set => SetField(ref _ecsScopeNetmask, value);
    }

    public bool IsTcpEnabled
    {
        get => _isTcpEnabled;
        set => SetField(ref _isTcpEnabled, value);
    }

    public bool IsUdpEnabled
    {
        get => _isUdpEnabled;
        set => SetField(ref _isUdpEnabled, value);
    }

    #endregion

    #region Ignore

    private IPAddress? _ip;
    public const string DefaultDnsServer = @"208.67.222.222";
    public const ushort DefaultPort = 53;
    public const string DefaultTlsDnsServer = @"208.67.222.222";
    public const ushort DefaultTlsPort = 853;

    #endregion

    #region 构造函数

    [JsonConstructor]
    public DnsClient()
    {
        _ip = null;

        _enable = true;
        _dnsType = DnsType.Default;
        _ipv6First = false;
        _dnsServer = DefaultDnsServer;
        _port = DefaultPort;
        _timeout = 10000;
        _isEDnsEnabled = false;
        _ecsIp = DefaultDnsServer;
        _ecsSourceNetmask = 32;
        _ecsScopeNetmask = 0;
        _isTcpEnabled = true;
        _isUdpEnabled = true;
    }

    public DnsClient(DnsType type) : this()
    {
        _dnsType = type;
        switch (type)
        {
            case DnsType.Default:
            {
                _dnsServer = DefaultDnsServer;
                _port = DefaultPort;
                break;
            }
            case DnsType.DnsOverTls:
            {
                _dnsServer = DefaultTlsDnsServer;
                _port = DefaultTlsPort;
                break;
            }
        }
    }

    #endregion

    #region Private Method

    private static bool IsIp(string? str)
    {
        return IPAddress.TryParse(str, out var ip) && ip.ToString() == str;
    }

    private bool IsValidDns(string? dns)
    {
        return DnsType == DnsType.DnsOverTls || IsIp(dns);
    }

    private static async Task<IPAddress?> QueryBaseAAsync(ARSoft.Tools.Net.Dns.DnsClient client, DomainName domain, DnsQueryOptions options, CancellationToken ct)
    {
        DnsMessage? message = null;
        try
        {
            message = await client.ResolveAsync(domain, RecordType.A, RecordClass.INet, options, ct);
        }
        catch
        {
            // ignored
        }
        return message?.AnswerRecords?.OfType<ARecord>().Select(answerRecord => answerRecord.Address).FirstOrDefault();
    }

    private static async Task<IPAddress?> QueryBaseAaaaAsync(ARSoft.Tools.Net.Dns.DnsClient client, DomainName domain, DnsQueryOptions options, CancellationToken ct)
    {
        DnsMessage? message = null;
        try
        {
            message = await client.ResolveAsync(domain, RecordType.Aaaa, RecordClass.INet, options, ct);
        }
        catch
        {
            // ignored
        }
        return message?.AnswerRecords?.OfType<AaaaRecord>().Select(answerRecord => answerRecord.Address).FirstOrDefault();
    }

    private static async Task<IPAddress?> QueryBaseAsync(ARSoft.Tools.Net.Dns.DnsClient client, DomainName domain, DnsQueryOptions options, bool ipv6First, CancellationToken ct)
    {
        var res = await Task.WhenAll(
            QueryBaseAaaaAsync(client, domain, options, ct),
            QueryBaseAAsync(client, domain, options, ct));

        if (ipv6First)
        {
            return res[0] ?? res[1];
        }

        return res[1] ?? res[0];
    }

    private static async Task<IPAddress?> QueryBaseTlsAAsync(DnsOverTlsClient client, DomainName domain, DnsQueryOptions options, CancellationToken ct)
    {
        DnsMessage? message = null;
        try
        {
            message = await client.ResolveAsync(domain, RecordType.A, RecordClass.INet, options, ct);
        }
        catch
        {
            // ignored
        }
        return message?.AnswerRecords?.OfType<ARecord>().Select(answerRecord => answerRecord.Address).FirstOrDefault();
    }

    private static async Task<IPAddress?> QueryBaseTlsAaaaAsync(DnsOverTlsClient client, DomainName domain, DnsQueryOptions options, CancellationToken ct)
    {
        DnsMessage? message = null;
        try
        {
            message = await client.ResolveAsync(domain, RecordType.Aaaa, RecordClass.INet, options, ct);
        }
        catch
        {
            // ignored
        }
        return message?.AnswerRecords?.OfType<AaaaRecord>().Select(answerRecord => answerRecord.Address).FirstOrDefault();
    }

    private static async Task<IPAddress?> QueryBaseTlsAsync(DnsOverTlsClient client, DomainName domain, DnsQueryOptions options, bool ipv6First, CancellationToken ct)
    {
        if (ipv6First)
        {
            var res = await Task.WhenAll(QueryBaseTlsAaaaAsync(client, domain, options, ct), QueryBaseTlsAAsync(client, domain, options, ct));
            return res[0] ?? res[1];
        }
        else
        {
            var res = await Task.WhenAll(QueryBaseTlsAAsync(client, domain, options, ct));
            return res[0];
        }
    }

    #endregion

    public static async Task<IPAddress?> QueryIpAddressDefaultAsync(string host, bool ipv6First, CancellationToken ct)
    {
        IPAddress[] ips = await Dns.GetHostAddressesAsync(host, ct);

        if (ipv6First)
        {
            foreach (var ip in ips)
            {
                if (ip.AddressFamily is AddressFamily.InterNetworkV6)
                {
                    return ip;
                }
            }
        }

        return ips.FirstOrDefault();
    }

    public async Task<IPAddress?> QueryIpAddressAsync(string host, CancellationToken ct)
    {
        var domain = DomainName.Parse(host);
        var options = new DnsQueryOptions
        {
            IsEDnsEnabled = IsEDnsEnabled,
            IsRecursionDesired = true,
        };
        if (options.IsEDnsEnabled)
        {
            options.EDnsOptions = new OptRecord { Options = { new ClientSubnetOption(EcsSourceNetmask, EcsScopeNetmask, IPAddress.Parse(EcsIp)) } };
        }
        switch (DnsType)
        {
            case DnsType.Default:
            {
                var dnsClient = new ARSoft.Tools.Net.Dns.DnsClient(IPAddress.Parse(DnsServer), Timeout, Port)
                {
                    IsTcpEnabled = IsTcpEnabled,
                    IsUdpEnabled = IsUdpEnabled
                };
                return await QueryBaseAsync(dnsClient, domain, options, Ipv6First, ct);
            }
            case DnsType.DnsOverTls:
            {
                _ip ??= await QueryIpAddressDefaultAsync(DnsServer, Ipv6First, ct);
                if (_ip is null)
                {
                    return null;
                }
                var tlsServer = new TlsUpstreamServer
                {
                    IPAddress = _ip,
                    AuthName = DnsServer,
                    SslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12
                };
                var dnsClient = new DnsOverTlsClient(tlsServer, Timeout, Port);
                var res = await QueryBaseTlsAsync(dnsClient, domain, options, Ipv6First, ct);
                if (res is null)
                {
                    _ip = null;
                }
                return res;
            }
            default:
                return null;
        }
    }

}
