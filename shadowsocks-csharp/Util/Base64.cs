using System;
using System.Text;

namespace Shadowsocks.Util
{
    public static class Base64
    {
        public static string DecodeBase64(string val)
        {
            byte[] bytes = null;
            string data = val;
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    bytes = Convert.FromBase64String(val);
                }
                catch (FormatException)
                {
                    val += "=";
                }
            }
            if (bytes != null)
            {
                data = Encoding.UTF8.GetString(bytes);
            }
            return data;
        }

        public static string EncodeUrlSafeBase64(byte[] val, bool trim)
        {
            if (trim)
                return Convert.ToBase64String(val).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            else
                return Convert.ToBase64String(val).Replace('+', '-').Replace('/', '_');
        }

        public static byte[] DecodeUrlSafeBase64ToBytes(string val)
        {
            var data = val.Replace('-', '+').Replace('_', '/').PadRight(val.Length + (4 - val.Length % 4) % 4, '=');
            return Convert.FromBase64String(data);
        }

        public static string EncodeUrlSafeBase64(string val, bool trim = true)
        {
            return EncodeUrlSafeBase64(Encoding.UTF8.GetBytes(val), trim);
        }

        public static string DecodeUrlSafeBase64(string val)
        {
            return Encoding.UTF8.GetString(DecodeUrlSafeBase64ToBytes(val));
        }
    }
}
