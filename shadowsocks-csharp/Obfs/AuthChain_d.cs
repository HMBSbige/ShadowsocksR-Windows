using System.Collections.Generic;

namespace Shadowsocks.Obfs
{
    class AuthChain_d : AuthChain_c
    {
        public AuthChain_d(string method)
                : base(method)
        {

        }

        private static Dictionary<string, int[]> _obfs = new()
        {
            { "auth_chain_d", new[] { 1, 0, 1 } }
        };

        public static new List<string> SupportedObfs()
        {
            return new(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        protected void CheckAndPatchDataSize(List<int> data_list, xorshift128plus rd)
        {
            if (data_list[data_list.Count - 1] < 1300 && data_list.Count < 64)
            {
                data_list.Add((int)(rd.next() % 2340 % 2040 % 1440));
                CheckAndPatchDataSize(data_list, rd);
            }
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
            var old_len = data_list.Count;
            CheckAndPatchDataSize(data_list, rd);
            if (old_len != data_list.Count)
            {
                data_list.Sort();
            }
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

            if (other_data_size >= data_size_list0[data_size_list0.Length - 1])
            {
                return 0;
            }

            rd.init_from_bin(last_hash, datalength);
            var pos = FindPos(data_size_list0, other_data_size);
            var final_pos = pos + (int)(rd.next() % (ulong)(data_size_list0.Length - pos));
            return data_size_list0[final_pos] - other_data_size;
        }

    }
}
