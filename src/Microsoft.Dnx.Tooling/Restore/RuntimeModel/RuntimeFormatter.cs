using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Dnx.Tooling.Restore.RuntimeModel
{
    public class RuntimeFileFormatter
    {
        public RuntimeFile ReadRuntimeFile(string filePath)
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                using (var streamReader = new StreamReader(fileStream))
                {
                    return ReadRuntimeFile(streamReader);
                }
            }
        }

        public RuntimeFile ReadRuntimeFile(TextReader textReader)
        {
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return ReadRuntimeFile(JToken.Load(jsonReader));
            }
        }

        public void WriteRuntimeFile(string filePath, RuntimeFile runtimeFile)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                using (var textWriter = new StreamWriter(fileStream))
                {
                    using (var jsonWriter = new JsonTextWriter(textWriter))
                    {
                        jsonWriter.Formatting = Formatting.Indented;
                        var json = new JObject();
                        WriteRuntimeFile(json, runtimeFile);
                        json.WriteTo(jsonWriter);
                    }
                }
            }
        }

        public RuntimeFile ReadRuntimeFile(JToken json)
        {
            var file = new RuntimeFile();
            foreach (var runtimeSpec in EachProperty(json["runtimes"]).Select(ReadRuntimeSpec))
            {
                file.Runtimes.Add(runtimeSpec.Name, runtimeSpec);
            }
            return file;
        }

        private void WriteRuntimeFile(JObject json, RuntimeFile runtimeFile)
        {
            var runtimes = new JObject();
            json["runtimes"] = runtimes;
            foreach(var x in runtimeFile.Runtimes.Values)
            {
                WriteRuntimeSpec(runtimes, x);
            }
        }

        private void WriteRuntimeSpec(JObject json, RuntimeSpec data)
        {
            var value = new JObject();
            json[data.Name] = value;
            value["#import"] = new JArray(data.Import.Select(x => new JValue(x)));
            foreach (var x in data.Dependencies.Values)
            {
                WriteDependencySpec(value, x);
            }
        }

        private void WriteDependencySpec(JObject json, DependencySpec data)
        {
            var value = new JObject();
            json[data.Name] = value;
            foreach (var x in data.Implementations.Values)
            {
                WriteImplementationSpec(value, x);
            }
        }

        private void WriteImplementationSpec(JObject json, ImplementationSpec data)
        {
            json[data.Name] = new JValue(data.Version);
        }

        private RuntimeSpec ReadRuntimeSpec(KeyValuePair<string, JToken> json)
        {
            var runtime = new RuntimeSpec();
            runtime.Name = json.Key;
            foreach (var property in EachProperty(json.Value))
            {
                if (property.Key == "#import")
                {
                    var imports = property.Value as JArray;
                    foreach (var import in imports)
                    {
                        runtime.Import.Add(import.Value<string>());
                    }
                }
                else
                {
                    var dependency = ReadDependencySpec(property);
                    runtime.Dependencies.Add(dependency.Name, dependency);
                }
            }
            return runtime;
        }

        public DependencySpec ReadDependencySpec(KeyValuePair<string, JToken> json)
        {
            var dependency = new DependencySpec();
            dependency.Name = json.Key;
            foreach (var implementation in EachProperty(json.Value).Select(ReadImplementationSpec))
            {
                dependency.Implementations.Add(implementation.Name, implementation);
            }
            return dependency;
        }

        public ImplementationSpec ReadImplementationSpec(KeyValuePair<string, JToken> json)
        {
            var implementation = new ImplementationSpec();
            implementation.Name = json.Key;
            foreach (var property in EachProperty(json.Value, "version"))
            {
                if (property.Key == "version")
                {
                    implementation.Version = property.Value.ToString();
                }
            }
            return implementation;
        }

        private IEnumerable<KeyValuePair<string, JToken>> EachProperty(JToken json)
        {
            return (json as IEnumerable<KeyValuePair<string, JToken>>)
                ?? Enumerable.Empty<KeyValuePair<string, JToken>>();
        }

        private IEnumerable<KeyValuePair<string, JToken>> EachProperty(JToken json, string defaultPropertyName)
        {
            return (json as IEnumerable<KeyValuePair<string, JToken>>)
                ?? new[] { new KeyValuePair<string, JToken>(defaultPropertyName, json) };
        }

        private IEnumerable<JToken> EachArray(JToken json)
        {
            return (IEnumerable<JToken>)(json as JArray)
                ?? new[] { json };
        }

    }
}