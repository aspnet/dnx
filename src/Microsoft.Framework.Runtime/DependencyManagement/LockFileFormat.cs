// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Json;
using NuGet;

namespace Microsoft.Framework.Runtime.DependencyManagement
{
    public class LockFileFormat
    {
        public const int Version = -9997;
        public const string LockFileName = "project.lock.json";

        private string _currentLockFilePath;

        public LockFile Read(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                _currentLockFilePath = filePath;

                var result = Read(stream);

                _currentLockFilePath = null;

                return result;
            }
        }

        private LockFile Read(Stream stream)
        {
            try
            {
                var deserializer = new JsonDeserializer();
                var jobject = deserializer.Deserialize(stream) as JsonObject;

                if (jobject != null)
                {
                    return ReadLockFile(jobject);
                }
                else
                {
                    throw new InvalidDataException();
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

        private LockFile ReadLockFile(JsonObject cursor)
        {
            var lockFile = new LockFile();
            lockFile.Islocked = ReadBool(cursor, "locked", defaultValue: false);
            lockFile.Version = ReadInt(cursor, "version", defaultValue: int.MinValue);
            lockFile.Libraries = ReadObject(cursor.ValueAsJsonObject("libraries"), ReadLibrary);
            lockFile.Targets = ReadObject(cursor.ValueAsJsonObject("targets"), ReadTarget);
            lockFile.ProjectFileDependencyGroups = ReadObject(cursor.ValueAsJsonObject("projectFileDependencyGroups"), ReadProjectFileDependencyGroup);
            return lockFile;
        }

        private LockFileLibrary ReadLibrary(string property, JsonValue json)
        {
            var jobject = json as JsonObject;
            if (jobject == null)
            {
                throw FileFormatException.Create("The value type is not object.", json, _currentLockFilePath);
            }

            var library = new LockFileLibrary();
            var parts = property.Split(new[] { '/' }, 2);
            library.Name = parts[0];
            if (parts.Length == 2)
            {
                library.Version = SemanticVersion.Parse(parts[1]);
            }
            library.IsServiceable = ReadBool(jobject, "serviceable", defaultValue: false);
            library.Sha512 = ReadString(jobject.Value("sha512"));
            library.Files = ReadPathArray(jobject.Value("files"), ReadString);
            return library;
        }

        private LockFileTarget ReadTarget(string property, JsonValue json)
        {
            var jobject = json as JsonObject;
            if (jobject == null)
            {
                throw FileFormatException.Create("The value type is not object.", json, _currentLockFilePath);
            }

            var target = new LockFileTarget();
            var parts = property.Split(new[] { '/' }, 2);
            target.TargetFramework = new FrameworkName(parts[0]);
            if (parts.Length == 2)
            {
                target.RuntimeIdentifier = parts[1];
            }

            target.Libraries = ReadObject(jobject, ReadTargetLibrary);

            return target;
        }

        private LockFileTargetLibrary ReadTargetLibrary(string property, JsonValue json)
        {
            var jobject = json as JsonObject;
            if (jobject == null)
            {
                throw FileFormatException.Create("The value type is not object.", json, _currentLockFilePath);
            }

            var library = new LockFileTargetLibrary();

            var parts = property.Split(new[] { '/' }, 2);
            library.Name = parts[0];
            if (parts.Length == 2)
            {
                library.Version = SemanticVersion.Parse(parts[1]);
            }

            library.Dependencies = ReadObject(jobject.ValueAsJsonObject("dependencies"), ReadPackageDependency);
            library.FrameworkAssemblies = ReadArray(jobject.Value("frameworkAssemblies"), ReadFrameworkAssemblyReference);
            library.RuntimeAssemblies = ReadPathArray(jobject.Value("runtime"), ReadString);
            library.CompileTimeAssemblies = ReadPathArray(jobject.Value("compile"), ReadString);
            library.NativeLibraries = ReadPathArray(jobject.Value("native"), ReadString);

            return library;
        }

        private ProjectFileDependencyGroup ReadProjectFileDependencyGroup(string property, JsonValue json)
        {
            return new ProjectFileDependencyGroup(
                property,
                ReadArray(json, ReadString));
        }

        private PackageDependency ReadPackageDependency(string property, JsonValue json)
        {
            var versionStr = ReadString(json);
            return new PackageDependency(
                property,
                versionStr == null ? null : VersionUtility.ParseVersionSpec(versionStr));
        }

        private FrameworkAssemblyReference ReadFrameworkAssemblyReference(JsonValue json)
        {
            return new FrameworkAssemblyReference(ReadString(json));
        }

        private IList<TItem> ReadArray<TItem>(JsonValue json, Func<JsonValue, TItem> readItem)
        {
            if (json == null)
            {
                return new List<TItem>();
            }

            var jarray = json as JsonArray;
            if (jarray == null)
            {
                throw FileFormatException.Create("The value type is not array.", json, _currentLockFilePath);
            }

            var items = new List<TItem>();
            for (int i = 0; i < jarray.Count; ++i)
            {
                items.Add(readItem(jarray[i]));
            }
            return items;
        }

        private IList<string> ReadPathArray(JsonValue json, Func<JsonValue, string> readItem)
        {
            return ReadArray(json, readItem).Select(f => PathUtility.GetPathWithDirectorySeparator(f)).ToList();
        }

        private IList<TItem> ReadObject<TItem>(JsonObject json, Func<string, JsonValue, TItem> readItem)
        {
            if (json == null)
            {
                return new List<TItem>();
            }
            var items = new List<TItem>();
            foreach (var childKey in json.Keys)
            {
                items.Add(readItem(childKey, json.Value(childKey)));
            }
            return items;
        }

        private bool ReadBool(JsonObject cursor, string property, bool defaultValue)
        {
            var valueToken = cursor.Value(property) as JsonBoolean;
            if (valueToken == null)
            {
                return defaultValue;
            }

            return valueToken.Value;
        }

        private int ReadInt(JsonObject cursor, string property, int defaultValue)
        {
            var valueToken = cursor.Value(property) as JsonInteger;
            if (valueToken == null)
            {
                return defaultValue;
            }
            return valueToken.Value;
        }

        private string ReadString(JsonValue json)
        {
            if (json is JsonString)
            {
                return (json as JsonString).Value;
            }
            else
            {
                throw FileFormatException.Create("The value type is not string.", json, _currentLockFilePath);
            }
        }
    }
}