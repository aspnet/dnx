// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Dnx.Runtime.Json;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    internal class LockFileReader
    {
        public const string LockFileName = "project.lock.json";

        public LockFile Read(string filePath)
        {
            using (var stream = OpenFileStream(filePath))
            {
                try
                {
                    return Read(stream);
                }
                catch (FileFormatException ex)
                {
                    throw ex.WithFilePath(filePath);
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, filePath);
                }
            }
        }

        private static FileStream OpenFileStream(string filePath)
        {
            // Retry 3 times before re-throw the exception.
            // It mitigates the race condition when DTH read lock file while VS is restoring projects.

            int retry = 3;
            while (true)
            {
                try
                {
                    return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                catch (Exception)
                {
                    if (retry > 0)
                    {
                        retry--;
                        Thread.Sleep(100);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

        }

        internal LockFile Read(Stream stream)
        {
            try
            {
                var reader = new StreamReader(stream);
                var jobject = JsonDeserializer.Deserialize(reader) as JsonObject;

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
            lockFile.Targets = ReadObject(cursor.ValueAsJsonObject("targets"), ReadTarget);
            lockFile.ProjectFileDependencyGroups = ReadObject(cursor.ValueAsJsonObject("projectFileDependencyGroups"), ReadProjectFileDependencyGroup);
            ReadLibrary(cursor.ValueAsJsonObject("libraries"), lockFile);

            return lockFile;
        }

        private void ReadLibrary(JsonObject json, LockFile lockFile)
        {
            if (json == null)
            {
                return;
            }

            foreach (var key in json.Keys)
            {
                var value = json.ValueAsJsonObject(key);
                if (value == null)
                {
                    throw FileFormatException.Create("The value type is not object.", json.Value(key));
                }

                var parts = key.Split(new[] { '/' }, 2);
                var name = parts[0];
                var version = parts.Length == 2 ? SemanticVersion.Parse(parts[1]) : null;

                var type = value.ValueAsString("type")?.Value;

                if (type == null || type == "package")
                {
                    lockFile.PackageLibraries.Add(new LockFilePackageLibrary
                    {
                        Name = name,
                        Version = version,
                        IsServiceable = ReadBool(value, "serviceable", defaultValue: false),
                        Sha512 = ReadString(value.Value("sha512")),
                        Files = ReadPathArray(value.Value("files"), ReadString)
                    });
                }
                else if (type == "project")
                {
                    lockFile.ProjectLibraries.Add(new LockFileProjectLibrary
                    {
                        Name = name,
                        Version = version,
                        Path = ReadString(value.Value("path"))
                    });
                }
            }
        }

        private LockFileTarget ReadTarget(string property, JsonValue json)
        {
            var jobject = json as JsonObject;
            if (jobject == null)
            {
                throw FileFormatException.Create("The value type is not an object.", json);
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
                throw FileFormatException.Create("The value type is not an object.", json);
            }

            var library = new LockFileTargetLibrary();

            var parts = property.Split(new[] { '/' }, 2);
            library.Name = parts[0];
            if (parts.Length == 2)
            {
                library.Version = SemanticVersion.Parse(parts[1]);
            }

            library.Type = jobject.ValueAsString("type");
            library.Dependencies = ReadObject(jobject.ValueAsJsonObject("dependencies"), ReadPackageDependency);
            library.FrameworkAssemblies = new HashSet<string>(ReadArray(jobject.Value("frameworkAssemblies"), ReadFrameworkAssemblyReference), StringComparer.OrdinalIgnoreCase);
            library.RuntimeAssemblies = ReadObject(jobject.ValueAsJsonObject("runtime"), ReadFileItem);
            library.CompileTimeAssemblies = ReadObject(jobject.ValueAsJsonObject("compile"), ReadFileItem);
            library.ResourceAssemblies = ReadObject(jobject.ValueAsJsonObject("resource"), ReadFileItem);
            library.NativeLibraries = ReadObject(jobject.ValueAsJsonObject("native"), ReadFileItem);

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

        private LockFileItem ReadFileItem(string property, JsonValue json)
        {
            var item = new LockFileItem { Path = property };
            var jobject = json as JsonObject;

            if (jobject != null)
            {
                foreach (var subProperty in jobject.Keys)
                {
                    item.Properties[subProperty] = jobject.ValueAsString(subProperty);
                }
            }
            return item;
        }

        private string ReadFrameworkAssemblyReference(JsonValue json)
        {
            return ReadString(json);
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
                throw FileFormatException.Create("The value type is not array.", json);
            }

            var items = new List<TItem>();
            for (int i = 0; i < jarray.Length; ++i)
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
            var number = cursor.Value(property) as JsonNumber;
            if (number == null)
            {
                return defaultValue;
            }

            try
            {
                var resultInInt = Convert.ToInt32(number.Raw);
                return resultInInt;
            }
            catch (Exception ex)
            {
                // FormatException or OverflowException
                throw FileFormatException.Create(ex, cursor);
            }
        }

        private string ReadString(JsonValue json)
        {
            if (json is JsonString)
            {
                return (json as JsonString).Value;
            }
            else if (json is JsonNull)
            {
                return null;
            }
            else
            {
                throw FileFormatException.Create("The value type is not string.", json);
            }
        }
    }
}