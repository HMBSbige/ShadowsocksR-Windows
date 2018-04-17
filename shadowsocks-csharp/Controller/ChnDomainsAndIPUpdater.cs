
namespace Shadowsocks.Controller
{
	public class ChnDomainsAndIPUpdater
	{
		private const string CNIP_URL = @"https://ftp.apnic.net/apnic/stats/apnic/delegated-apnic-latest";
		private const string CNDOMAINS_URL = @"https://raw.githubusercontent.com/felixonmars/dnsmasq-china-list/master/accelerated-domains.china.conf";

		private const string SS_WHITE_TEMPLATE_URL =
				@"https://raw.githubusercontent.com/HMBSbige/Text_Translation/master/ShadowsocksR/ss_white_temp.pac";

		private const string USER_AGENT = @"Mozilla/5.0 (Windows NT 5.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/35.0.3319.102 Safari/537.36";

		private static string PAC_FILE = PACServer.PAC_FILE;
		private static string USER_RULE_FILE = PACServer.WHITELIST_FILE;

		private static string template = null;


	}
}