using System.Collections.Generic;

namespace Shadowsocks.Obfs
{
    class AuthChain_e : AuthChain_d
    {
        public AuthChain_e(string method)
                : base(method)
        {

        }

        private static Dictionary<string, int[]> _obfs = new()
        {
            { "auth_chain_e", new[] { 1, 0, 1 } }
        };

        public static new List<string> SupportedObfs()
        {
            return new(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        protected override int GetRandLen(int datalength, xorshift128plus rd, byte[] last_hash)
        {
            rd.init_from_bin(last_hash, datalength);
            var other_data_size = datalength + Server.overhead;

            if (other_data_size >= data_size_list0[data_size_list0.Length - 1])
            {
                return 0;
            }

            var pos = FindPos(data_size_list0, other_data_size);
            return data_size_list0[pos] - other_data_size;
        }

    }
}
