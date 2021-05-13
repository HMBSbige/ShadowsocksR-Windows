using System.Collections.Generic;

namespace Shadowsocks.Obfs
{
    class AuthChain_b : AuthChain_a
    {
        public AuthChain_b(string method)
                : base(method)
        {

        }

        private static Dictionary<string, int[]> _obfs = new()
        {
            { "auth_chain_b", new[] { 1, 0, 1 } }
        };

        protected int[] data_size_list;
        protected int[] data_size_list2;

        public static new List<string> SupportedObfs()
        {
            return new(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        protected virtual void InitDataSizeList()
        {
            var rd = new xorshift128plus();
            rd.init_from_bin(Server.key);
            var len = (int)(rd.next() % 8 + 4);
            var data_list = new List<int>();
            for (var i = 0; i < len; ++i)
            {
                data_list.Add((int)(rd.next() % 2340 % 2040 % 1440));
            }
            data_list.Sort();
            data_size_list = data_list.ToArray();

            len = (int)(rd.next() % 16 + 8);
            data_list.Clear();
            for (var i = 0; i < len; ++i)
            {
                data_list.Add((int)(rd.next() % 2340 % 2040 % 1440));
            }
            data_list.Sort();
            data_size_list2 = data_list.ToArray();
        }

        public override void SetServerInfo(ServerInfo serverInfo)
        {
            Server = serverInfo;
            InitDataSizeList();
        }

        protected int FindPos(int[] arr, int key)
        {
            var low = 0;
            var high = arr.Length - 1;

            if (key > arr[high])
            {
                return arr.Length;
            }

            while (low < high)
            {
                var middle = (low + high) / 2;
                if (key > arr[middle])
                {
                    low = middle + 1;
                }
                else if (key <= arr[middle])
                {
                    high = middle;
                }
            }
            return low;
        }

        protected override int GetRandLen(int datalength, xorshift128plus rd, byte[] last_hash)
        {
            if (datalength >= 1440)
            {
                return 0;
            }

            rd.init_from_bin(last_hash, datalength);

            var pos = FindPos(data_size_list, datalength + Server.overhead);
            var final_pos = pos + (int)(rd.next() % (ulong)data_size_list.Length);
            if (final_pos < data_size_list.Length)
            {
                return data_size_list[final_pos] - datalength - Server.overhead;
            }

            pos = FindPos(data_size_list2, datalength + Server.overhead);
            final_pos = pos + (int)(rd.next() % (ulong)data_size_list2.Length);
            if (final_pos < data_size_list2.Length)
            {
                return data_size_list2[final_pos] - datalength - Server.overhead;
            }
            if (final_pos < pos + data_size_list2.Length - 1)
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

    }
}
