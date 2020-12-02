using System.Text.Encodings.Web;
using System.Text.Json;

namespace Shadowsocks.Util
{
    public static class JsonUtils
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static readonly JsonSerializerOptions StrictOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Default
        };

        public static string Serialize(object obj, bool strict)
        {
            return JsonSerializer.Serialize(obj, strict ? StrictOptions : Options);
        }
    }
}
