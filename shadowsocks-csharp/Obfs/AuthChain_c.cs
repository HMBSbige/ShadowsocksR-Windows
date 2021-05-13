using System.Collections.Generic;

namespace Shadowsocks.Obfs
{
    class AuthChain_c : AuthChain_b
    {
        public AuthChain_c(string method)
                : base(method)
        {

        }

        private static Dictionary<string, int[]> _obfs = new()
        {
            { "auth_chain_c", new[] { 1, 0, 1 } }
        };

        protected int[] data_size_list0;

        public static new List<string> SupportedObfs()
        {
            return new(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        protected override void InitDataSizeList()
        {
            var rd = new xorshift128plus();
            rd.init_from_bin(Server.key);
            var len = (int)(rd.next() % (8 + 16) + (4 + 8));
            var data_list = new List<int>();
            for (var i = 0; i < len; ++i)
            {
                data_list.Add((int)(rd.next() % 2340 % 2040 % 1440));
            }
            data_list.Sort();
            data_size_list0 = data_list.ToArray();
        }

        public override void SetServerInfo(ServerInfo serverInfo)
        {
            Server = serverInfo;
            InitDataSizeList();
        }

        protected override int GetRandLen(int datalength, xorshift128plus rd, byte[] last_hash)
        {
            var other_data_size = datalength + Server.overhead;

            // 一定要在random使用前初始化，以保证服务器与客户端同步，保证包大小验证结果正确
            rd.init_from_bin(last_hash, datalength);
            if (other_data_size >= data_size_list0[data_size_list0.Length - 1])
            {
                if (datalength >= 1440)
                {
                    return 0;
                }

                if (datalength > 1300)
                {
                    return (int)(rd.next() % 31);
                }

                if (datalength > 900)
                {
                    return (int)(rd.next() % 127);
                }

                if (datalength > 400)
                {
                    return (int)(rd.next() % 521);
                }

                return (int)(rd.next() % 1021);
            }

            var pos = FindPos(data_size_list0, other_data_size);
            var final_pos = pos + (int)(rd.next() % (ulong)(data_size_list0.Length - pos));
            return data_size_list0[final_pos] - other_data_size;
        }

    }
}
