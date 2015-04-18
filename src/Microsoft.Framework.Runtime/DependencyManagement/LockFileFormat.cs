// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using NuGet;
using System.Runtime.Versioning;
using System.Linq;

namespace Microsoft.Framework.Runtime.DependencyManagement
{
    public class LockFileFormat
    {
        public const int Version = -9997;
        public const string LockFileName = "project.lock.json";

        public LockFile Read(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(stream);
            }
        }

        public LockFile Read(Stream stream)
        {
            using (var textReader = new StreamReader(stream))
            {
                try
                {
                    using (var jsonReader = new JsonTextReader(textReader))
                    {
                        while (jsonReader.TokenType != JsonToken.StartObject)
                        {
                            if (!jsonReader.Read())
                            {
                                throw new InvalidDataException();
                            }
                        }
                        var token = JToken.Load(jsonReader);
                        return ReadLockFile(token as JObject);
                    }
                }
                catch
                {
                    // Ran into parsing errors, mark it as unlocked and out-of-date
                    return new LockFile
                    {
                        Islocked = false,
                        Version = int.MinValue
                    };
                }
            }
        }

        public void Write(string filePath, LockFile lockFile)
        {
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Write(stream, lockFile);
            }
        }

        public void Write(Stream stream, LockFile lockFile)
        {
            using (var textWriter = new StreamWriter(stream))
            {
                using (var jsonWriter = new JsonTextWriter(textWriter))
                {
                    jsonWriter.Formatting = Formatting.Indented;

                    var json = WriteLockFile(lockFile);
                    json.WriteTo(jsonWriter);
                }
            }
        }

        private LockFile ReadLockFile(JObject cursor)
        {
            var lockFile = new LockFile();
            lockFile.Islocked = ReadBool(cursor, "locked", defaultValue: false);
            lockFile.Version = ReadInt(cursor, "version", defaultValue: int.MinValue);
            lockFile.Libraries = ReadObject(cursor["libraries"] as JObject, ReadLibrary);
            lockFile.Targets = ReadObject(cursor["targets"] as JObject, ReadTarget);
            lockFile.ProjectFileDependencyGroups = ReadObject(cursor["projectFileDependencyGroups"] as JObject, ReadProjectFileDependencyGroup);
            return lockFile;
        }

        private JObject WriteLockFile(LockFile lockFile)
        {
            var json = new JObject();
            json["locked"] = new JValue(lockFile.Islocked);
            json["version"] = new JValue(Version);
            json["targets"] = WriteObject(lockFile.Targets, WriteTarget);
            json["libraries"] = WriteObject(lockFile.Libraries, WriteLibrary);
            json["projectFileDependencyGroups"] = WriteObject(lockFile.ProjectFileDependencyGroups, WriteProjectFileDependencyGroup);
            return json;
        }

        private LockFileLibrary ReadLibrary(string property, JToken json)
        {
            var library = new LockFileLibrary();
            var parts = property.Split(new[] { '/' }, 2);
            library.Name = parts[0];
            if (parts.Length == 2)
            {
                library.Version = SemanticVersion.Parse(parts[1]);
            }
            library.IsServiceable = ReadBool(json, "serviceable", defaultValue: false);
            library.Sha512 = ReadString(json["sha512"]);
            library.Files = ReadPathArray(json["files"] as JArray, ReadString);
            return library;
        }

        private JProperty WriteLibrary(LockFileLibrary library)
        {
            var json = new JObject();
            if (library.IsServiceable)
            {
                WriteBool(json, "serviceable", library.IsServiceable);
            }
            json["sha512"] = WriteString(library.Sha512);
            WritePathArray(json, "files", library.Files, WriteString);
            return new JProperty(
                library.Name + "/" + library.Version.ToString(),
                json);
        }

        private JProperty WriteTarget(LockFileTarget target)
        {
            var json = WriteObject(target.Libraries, WriteTargetLibrary);

            var key = target.TargetFramework + (target.RuntimeIdentifier == null ? "" : "/" + target.RuntimeIdentifier);

            return new JProperty(key, json);
        }

        private LockFileTarget ReadTarget(string property, JToken json)
        {
            var target = new LockFileTarget();
            var parts = property.Split(new[] { '/' }, 2);
            target.TargetFramework = new FrameworkName(parts[0]);
            if (parts.Length == 2)
            {
                target.RuntimeIdentifier = parts[1];
            }

            target.Libraries = ReadObject(json as JObject, ReadTargetLibrary);

            return target;
        }

        private LockFileTargetLibrary ReadTargetLibrary(string property, JToken json)
        {
            var library = new LockFileTargetLibrary();

            var parts = property.Split(new[] { '/' }, 2);
            library.Name = parts[0];
            if (parts.Length == 2)
            {
                library.Version = SemanticVersion.Parse(parts[1]);
            }

            library.Dependencies = ReadObject(json["dependencies"] as JObject, ReadPackageDependency);
            library.FrameworkAssemblies = ReadArray(json["frameworkAssemblies"] as JArray, ReadFrameworkAssemblyReference);
            library.RuntimeAssemblies = ReadPathArray(json["runtime"] as JArray, ReadString);
            library.CompileTimeAssemblies = ReadPathArray(json["compile"] as JArray, ReadString);
            library.NativeLibraries = ReadPathArray(json["native"] as JArray, ReadString);

            return library;
        }

        private JProperty WriteTargetLibrary(LockFileTargetLibrary library)
        {
            var json = new JObject();

            if (library.Dependencies.Count > 0)
            {
                json["dependencies"] = WriteObject(library.Dependencies, WritePackageDependency);
            }

            if (library.FrameworkAssemblies.Count > 0)
            {
                json["frameworkAssemblies"] = WriteArray(library.FrameworkAssemblies, WriteFrameworkAssemblyReference);
            }

            if (library.CompileTimeAssemblies.Count > 0)
            {
                json["compile"] = WritePathArray(library.CompileTimeAssemblies, WriteString);
            }

            if (library.RuntimeAssemblies.Count > 0)
            {
                json["runtime"] = WritePathArray(library.RuntimeAssemblies, WriteString);
            }

            if (library.NativeLibraries.Count > 0)
            {
                json["native"] = WritePathArray(library.NativeLibraries, WriteString);
            }

            return new JProperty(library.Name + "/" + library.Version, json);
        }

        private ProjectFileDependencyGroup ReadProjectFileDependencyGroup(string property, JToken json)
        {
            return new ProjectFileDependencyGroup(
                property,
                ReadArray(json as JArray, ReadString));
        }

        private JProperty WriteProjectFileDependencyGroup(ProjectFileDependencyGroup frameworkInfo)
        {
            return new JProperty(
                frameworkInfo.FrameworkName,
                WriteArray(frameworkInfo.Dependencies, WriteString));
        }

        private IList<FrameworkAssemblyReference> ReadFrameworkAssemblies(JObject json)
        {
            var frameworkSets = ReadObject(json, (property, child) => new
            {
                FrameworkName = property,
                AssemblyNames = ReadArray(child as JArray, ReadString)
            });

            return frameworkSets.SelectMany(frameworkSet =>
            {
                if (frameworkSet.FrameworkName == "*")
                {
                    return frameworkSet.AssemblyNames.Select(name => new FrameworkAssemblyReference(name));
                }
                else
                {
                    var supportedFrameworks = new[] { new FrameworkName(frameworkSet.FrameworkName) };
                    return frameworkSet.AssemblyNames.Select(name => new FrameworkAssemblyReference(name, supportedFrameworks));
                }
            }).ToList();
        }

        private void WriteFrameworkAssemblies(JToken json, string property, IList<FrameworkAssemblyReference> frameworkAssemblies)
        {
            if (frameworkAssemblies.Any())
            {
                json[property] = WriteFrameworkAssemblies(frameworkAssemblies);
            }
        }

        private JToken WriteFrameworkAssemblies(IList<FrameworkAssemblyReference> frameworkAssemblies)
        {
            var groups = frameworkAssemblies.SelectMany(x =>
            {
                if (x.SupportedFrameworks.Any())
                {
                    return x.SupportedFrameworks.Select(xx => new { x.AssemblyName, FrameworkName = xx });
                }
                else
                {
                    return new[] { new { x.AssemblyName, FrameworkName = default(FrameworkName) } };
                }
            }).GroupBy(x => x.FrameworkName);

            return WriteObject(groups, group =>
            {
                return new JProperty(group.Key.ToStringSafe() ?? "*", group.Select(x => new JValue(x.AssemblyName)));
            });
        }

        private PackageDependencySet ReadPackageDependencySet(string property, JToken json)
        {
            var targetFramework = string.Equals(property, "*") ? null : new FrameworkName(property);
            return new PackageDependencySet(
                targetFramework,
                ReadObject(json as JObject, ReadPackageDependency));
        }

        private JProperty WritePackageDependencySet(PackageDependencySet item)
        {
            return new JProperty(
                item.TargetFramework.ToStringSafe() ?? "*",
                WriteObject(item.Dependencies, WritePackageDependency));
        }


        private PackageDependency ReadPackageDependency(string property, JToken json)
        {
            var versionStr = json.Value<string>();
            return new PackageDependency(
                property,
                versionStr == null ? null : VersionUtility.ParseVersionSpec(versionStr));
        }

        private JProperty WritePackageDependency(PackageDependency item)
        {
            return new JProperty(
                item.Id,
                WriteString(item.VersionSpec.ToStringSafe()));
        }

        private FrameworkAssemblyReference ReadFrameworkAssemblyReference(JToken json)
        {
            return new FrameworkAssemblyReference(json.Value<string>());
        }

        private JToken WriteFrameworkAssemblyReference(FrameworkAssemblyReference item)
        {
            return JToken.FromObject(item.AssemblyName);
        }

        private PackageReferenceSet ReadPackageReferenceSet(JToken json)
        {
            var frameworkName = json["targetFramework"].ToStringSafe();
            return new PackageReferenceSet(
                string.IsNullOrEmpty(frameworkName) ? null : new FrameworkName(frameworkName),
                ReadArray(json["references"] as JArray, ReadString));
        }

        private JToken WritePackageReferenceSet(PackageReferenceSet item)
        {
            var json = new JObject();
            json["targetFramework"] = item.TargetFramework.ToStringSafe();
            json["references"] = WriteArray(item.References, WriteString);
            return json;
        }

        private IPackageFile ReadPackageFile(string property, JToken json)
        {
            var file = new LockFilePackageFile();
            file.Path = PathUtility.GetPathWithDirectorySeparator(property);
            return file;
        }

        private JProperty WritePackageFile(IPackageFile item)
        {
            var json = new JObject();
            return new JProperty(PathUtility.GetPathWithForwardSlashes(item.Path), new JObject());
        }

        private IList<TItem> ReadArray<TItem>(JArray json, Func<JToken, TItem> readItem)
        {
            if (json == null)
            {
                return new List<TItem>();
            }
            var items = new List<TItem>();
            foreach (var child in json)
            {
                items.Add(readItem(child));
            }
            return items;
        }

        private IList<string> ReadPathArray(JArray json, Func<JToken, string> readItem)
        {
            return ReadArray(json, readItem).Select(f => PathUtility.GetPathWithDirectorySeparator(f)).ToList();
        }

        private void WriteArray<TItem>(JToken json, string property, IEnumerable<TItem> items, Func<TItem, JToken> writeItem)
        {
            if (items.Any())
            {
                json[property] = WriteArray(items, writeItem);
            }
        }

        private void WritePathArray(JToken json, string property, IEnumerable<string> items, Func<string, JToken> writeItem)
        {
            WriteArray(json, property, items.Select(f => PathUtility.GetPathWithForwardSlashes(f)), writeItem);
        }

        private JArray WriteArray<TItem>(IEnumerable<TItem> items, Func<TItem, JToken> writeItem)
        {
            var array = new JArray();
            foreach (var item in items)
            {
                array.Add(writeItem(item));
            }
            return array;
        }

        private JArray WritePathArray(IEnumerable<string> items, Func<string, JToken> writeItem)
        {
            return WriteArray(items.Select(f => PathUtility.GetPathWithForwardSlashes(f)), writeItem);
        }

        private IList<TItem> ReadObject<TItem>(JObject json, Func<string, JToken, TItem> readItem)
        {
            if (json == null)
            {
                return new List<TItem>();
            }
            var items = new List<TItem>();
            foreach (var child in json)
            {
                items.Add(readItem(child.Key, child.Value));
            }
            return items;
        }

        private void WriteObject<TItem>(JToken json, string property, IEnumerable<TItem> items, Func<TItem, JProperty> writeItem)
        {
            if (items.Any())
            {
                json[property] = WriteObject(items, writeItem);
            }
        }

        private JObject WriteObject<TItem>(IEnumerable<TItem> items, Func<TItem, JProperty> writeItem)
        {
            var array = new JObject();
            foreach (var item in items)
            {
                array.Add(writeItem(item));
            }
            return array;
        }

        private bool ReadBool(JToken cursor, string property, bool defaultValue)
        {
            var valueToken = cursor[property];
            if (valueToken == null)
            {
                return defaultValue;
            }
            return valueToken.Value<bool>();
        }

        private int ReadInt(JToken cursor, string property, int defaultValue)
        {
            var valueToken = cursor[property];
            if (valueToken == null)
            {
                return defaultValue;
            }
            return valueToken.Value<int>();
        }

        private string ReadString(JToken json)
        {
            return json.Value<string>();
        }

        private SemanticVersion ReadSemanticVersion(JToken json, string property)
        {
            var valueToken = json[property];
            if (valueToken == null)
            {
                throw new Exception(string.Format("TODO: lock file missing required property {0}", property));
            }
            return SemanticVersion.Parse(valueToken.Value<string>());
        }

        private void WriteBool(JToken token, string property, bool value)
        {
            token[property] = new JValue(value);
        }

        private JToken WriteString(string item)
        {
            return item != null ? new JValue(item) : JValue.CreateNull();
        }

        private FrameworkName ReadFrameworkName(JToken json)
        {
            return json == null ? null : new FrameworkName(json.Value<string>());
        }
        private JToken WriteFrameworkName(FrameworkName item)
        {
            return item != null ? new JValue(item.ToString()) : JValue.CreateNull();
        }

        class LockFilePackageFile : IPackageFile
        {
            public string EffectivePath
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public string Path { get; set; }

            public IEnumerable<FrameworkName> SupportedFrameworks
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public FrameworkName TargetFramework
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public Stream GetStream()
            {
                throw new NotImplementedException();
            }
        }
    }
}