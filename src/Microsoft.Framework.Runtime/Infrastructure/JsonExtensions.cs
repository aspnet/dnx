using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime.Infrastructure
{
    internal static class JsonExtensions
    {
        public static string[] ValueAsStringArray(this JObject obj, string key)
        {
            return (obj.Value<JArray>(key)).Select(v => v.Value<string>())
                                           .ToArray();
        }
    }
}