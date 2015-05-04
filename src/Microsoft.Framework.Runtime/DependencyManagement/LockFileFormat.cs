// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet;

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

        private LockFile Read(Stream stream)
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

        private ProjectFileDependencyGroup ReadProjectFileDependencyGroup(string property, JToken json)
        {
            return new ProjectFileDependencyGroup(
                property,
                ReadArray(json as JArray, ReadString));
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

        private PackageDependencySet ReadPackageDependencySet(string property, JToken json)
        {
            var targetFramework = string.Equals(property, "*") ? null : new FrameworkName(property);
            return new PackageDependencySet(
                targetFramework,
                ReadObject(json as JObject, ReadPackageDependency));
        }

        private PackageDependency ReadPackageDependency(string property, JToken json)
        {
            var versionStr = json.Value<string>();
            return new PackageDependency(
                property,
                versionStr == null ? null : VersionUtility.ParseVersionSpec(versionStr));
        }

        private FrameworkAssemblyReference ReadFrameworkAssemblyReference(JToken json)
        {
            return new FrameworkAssemblyReference(json.Value<string>());
        }

        private PackageReferenceSet ReadPackageReferenceSet(JToken json)
        {
            var frameworkName = json["targetFramework"].ToStringSafe();
            return new PackageReferenceSet(
                string.IsNullOrEmpty(frameworkName) ? null : new FrameworkName(frameworkName),
                ReadArray(json["references"] as JArray, ReadString));
        }

        private IPackageFile ReadPackageFile(string property, JToken json)
        {
            var file = new LockFilePackageFile();
            file.Path = PathUtility.GetPathWithDirectorySeparator(property);
            return file;
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

        private FrameworkName ReadFrameworkName(JToken json)
        {
            return json == null ? null : new FrameworkName(json.Value<string>());
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