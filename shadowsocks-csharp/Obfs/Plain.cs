using System.Collections.Generic;

namespace Shadowsocks.Obfs
{
    public class Plain : ObfsBase
    {
        public Plain(string method)
            : base(method)
        {
        }
        private static Dictionary<string, int[]> _obfs = new()
        {
            { "plain", new[] { 0, 0, 0 } },
            { "origin", new[] { 0, 0, 0 } }
        };

        public static List<string> SupportedObfs()
        {
            return new(_obfs.Keys);
        }

        public override Dictionary<string, int[]> GetObfs()
        {
            return _obfs;
        }

        public override byte[] ClientEncode(byte[] encryptdata, int datalength, out int outlength)
        {
            outlength = datalength;
            SentLength += outlength;
            return encryptdata;
        }
        public override byte[] ClientDecode(byte[] encryptdata, int datalength, out int outlength, out bool needsendback)
        {
            outlength = datalength;
            needsendback = false;
            return encryptdata;
        }
    }
}
