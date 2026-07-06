using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace LookupImportPlus.Services
{
    /// <summary>
    /// Shared JSON settings for persistence and the template manifest. camelCase
    /// property names + camelCase string enums make the serialized shape match
    /// the TS app's JSON closely and keep the manifest hash deterministic.
    /// </summary>
    public static class Json
    {
        public static readonly JsonSerializerSettings Settings = Build(indented: false);
        public static readonly JsonSerializerSettings Indented = Build(indented: true);

        private static JsonSerializerSettings Build(bool indented)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = indented ? Formatting.Indented : Formatting.None
            };
            settings.Converters.Add(new StringEnumConverter(new CamelCaseNamingStrategy(), allowIntegerValues: false));
            return settings;
        }

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
    }
}
