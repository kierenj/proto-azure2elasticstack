using System;
using Newtonsoft.Json;

namespace azure2elasticstack
{
    public static class JsonSerialisationUtilities
    {
        public static JsonSerializerSettings DefaultSettings = new JsonSerializerSettings
        {
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            TypeNameHandling = TypeNameHandling.All,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc
        };

        public static JsonSerializerSettings CondensedSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        public static JsonSerializerSettings ComparableSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            Formatting = Formatting.None,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects
        };

        public static string ToCondensedJsonString(this object item)
        {
            try
            {
                return JsonConvert.SerializeObject(item, CondensedSettings);
            }
            catch (JsonException ex)
            {
                throw new Exception("Error serialising JSON - see inner exception for details", ex);
            }
        }

        public static string ToComparableJsonString(this object item)
        {
            try
            {
                return JsonConvert.SerializeObject(item, ComparableSettings);
            }
            catch (JsonException ex)
            {
                throw new Exception("Error serialising JSON - see inner exception for details", ex);
            }
        }

        public static bool JsonEqualTo<T>(this T item, T other)
        {
            return item.ToComparableJsonString() == other.ToComparableJsonString();
        }

        public static string ToJsonString(this object item)
        {
            try
            {
                return JsonConvert.SerializeObject(item, DefaultSettings);
            }
            catch (JsonException ex)
            {
                throw new Exception("Error serialising JSON - see inner exception for details", ex);
            }
        }

        public static T DeserialiseJson<T>(this string json)
        {
            try
            {
                if (json == null) return default(T);
                return JsonConvert.DeserializeObject<T>(json, DefaultSettings);
            }
            catch (JsonException ex)
            {
                throw new Exception("Error deserialising JSON - see inner exception for details", ex);
            }
        }
    }
}