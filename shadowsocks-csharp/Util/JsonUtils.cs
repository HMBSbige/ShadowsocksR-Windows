#if IsDotNetCore
using System.Text.Encodings.Web;
using System.Text.Json;
#else
using Newtonsoft.Json;
#endif

namespace Shadowsocks.Util
{
    public static class JsonUtils
    {
#if IsDotNetCore
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static readonly JsonSerializerOptions StrictOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Default
        };
#endif

        public static string Serialize(object obj, bool strict)
        {
#if IsDotNetCore
            return JsonSerializer.Serialize(obj, strict ? StrictOptions : Options);
#else
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
#endif
        }

        public static T Deserialize<T>(string value)
        {
#if IsDotNetCore
            return JsonSerializer.Deserialize<T>(value);
#else
            return JsonConvert.DeserializeObject<T>(value);
#endif
        }
    }
}
