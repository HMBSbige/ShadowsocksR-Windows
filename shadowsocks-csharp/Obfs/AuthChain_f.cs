using System;
using System.Collections.Generic;

namespace Shadowsocks.Obfs
{
    class AuthChain_f : AuthChain_e
    {
        public AuthChain_f(string method)
                : base(method)
        {

        }

        private static Dictionary<string, int[]> _obfs = new()
        {
            { "auth_chain_f", new[] { 1, 0, 1 } }
        };

        public static new List<string> SupportedObfs()
        {
            return new(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        protected ulong key_change_interval = 60 * 60 * 24;    // a day by second
        protected ulong key_change_datetime_key;
        protected List<byte> key_change_datetime_key_bytes = new();

        protected override void InitDataSizeList()
        {
            var rd = new xorshift128plus();
            var newKey = new byte[Server.key.Length];
            Server.key.CopyTo(newKey, 0);
            for (var i = 0; i < 8; i++)
            {
                newKey[i] ^= key_change_datetime_key_bytes[i];
            }
            rd.init_from_bin(newKey);
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
            var protocalParams = serverInfo.param;
            if (protocalParams != "")
            {
                if (-1 != protocalParams.IndexOf("#", StringComparison.Ordinal))
                {
                    protocalParams = protocalParams.Split('#')[1];
                }

                if (ulong.TryParse(protocalParams, out var interval))
                {
                    key_change_interval = interval;
                }
            }
        }

        public override void OnInitAuthData(ulong unixTimestamp)
        {
            key_change_datetime_key = unixTimestamp / key_change_interval;
            for (var i = 7; i > -1; --i)
            {
                var b = (byte)(key_change_datetime_key >> (8 * i) & 0xFF);
                key_change_datetime_key_bytes.Add(b);
            }
            InitDataSizeList();
        }

    }
}
