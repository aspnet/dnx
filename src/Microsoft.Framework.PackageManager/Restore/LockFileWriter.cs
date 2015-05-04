// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.DependencyManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager.Restore
{
    internal class LockFileWriter
    {
        public void Write(string filePath, LockFile lockFile)
        {
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Write(stream, lockFile);
            }
        }

        private void Write(Stream stream, LockFile lockFile)
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

        private JObject WriteLockFile(LockFile lockFile)
        {
            var json = new JObject();
            json["locked"] = new JValue(lockFile.Islocked);
            json["version"] = new JValue(LockFileFormat.Version);
            json["targets"] = WriteObject(lockFile.Targets, WriteTarget);
            json["libraries"] = WriteObject(lockFile.Libraries, WriteLibrary);
            json["projectFileDependencyGroups"] = WriteObject(lockFile.ProjectFileDependencyGroups, WriteProjectFileDependencyGroup);
            return json;
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

        private JProperty WriteProjectFileDependencyGroup(ProjectFileDependencyGroup frameworkInfo)
        {
            return new JProperty(
                frameworkInfo.FrameworkName,
                WriteArray(frameworkInfo.Dependencies, WriteString));
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
                return new JProperty(group.Key?.ToString() ?? "*", group.Select(x => new JValue(x.AssemblyName)));
            });
        }

        private void WriteFrameworkAssemblies(JToken json, string property, IList<FrameworkAssemblyReference> frameworkAssemblies)
        {
            if (frameworkAssemblies.Any())
            {
                json[property] = WriteFrameworkAssemblies(frameworkAssemblies);
            }
        }

        private JProperty WritePackageDependencySet(PackageDependencySet item)
        {
            return new JProperty(
                item.TargetFramework?.ToString() ?? "*",
                WriteObject(item.Dependencies, WritePackageDependency));
        }

        private JProperty WritePackageDependency(PackageDependency item)
        {
            return new JProperty(
                item.Id,
                WriteString(item.VersionSpec?.ToString()));
        }

        private JToken WriteFrameworkAssemblyReference(FrameworkAssemblyReference item)
        {
            return new JValue(item.AssemblyName);
        }

        private JToken WritePackageReferenceSet(PackageReferenceSet item)
        {
            var json = new JObject();
            json["targetFramework"] = item.TargetFramework?.ToString();
            json["references"] = WriteArray(item.References, WriteString);
            return json;
        }

        private JProperty WritePackageFile(IPackageFile item)
        {
            var json = new JObject();
            return new JProperty(PathUtility.GetPathWithForwardSlashes(item.Path), new JObject());
        }

        private void WriteArray<TItem>(JToken json, string property, IEnumerable<TItem> items, Func<TItem, JToken> writeItem)
        {
            if (items.Any())
            {
                json[property] = WriteArray(items, writeItem);
            }
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

        private void WritePathArray(JToken json, string property, IEnumerable<string> items, Func<string, JToken> writeItem)
        {
            WriteArray(json, property, items.Select(f => PathUtility.GetPathWithForwardSlashes(f)), writeItem);
        }

        private JArray WritePathArray(IEnumerable<string> items, Func<string, JToken> writeItem)
        {
            return WriteArray(items.Select(f => PathUtility.GetPathWithForwardSlashes(f)), writeItem);
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

        private void WriteBool(JToken token, string property, bool value)
        {
            token[property] = new JValue(value);
        }

        private JToken WriteString(string item)
        {
            return item != null ? new JValue(item) : JValue.CreateNull();
        }

        private JToken WriteFrameworkName(FrameworkName item)
        {
            return item != null ? new JValue(item.ToString()) : JValue.CreateNull();
        }
    }
}
