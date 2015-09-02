using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Microsoft.Dnx.Testing
{
    public class JsonUtils
    {
        public static string NormalizeJson(string json)
        {
            return JObject.Parse(json).ToString();
        }

        public static string LoadNormalizedJson(string path)
        {
            return NormalizeJson(File.ReadAllText(path));
        }

        public static void UpdateJson(string path, Action<JObject> action)
        {
            var json = JObject.Parse(File.ReadAllText(path));
            action(json);
            File.WriteAllText(path, json.ToString());
        }
    }
}
