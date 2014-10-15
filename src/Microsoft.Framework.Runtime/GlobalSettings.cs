// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class GlobalSettings
    {
        public const string GlobalFileName = "global.json";

        public IList<string> SourcePaths { get; private set; }
        public IDictionary<Library, string> PackageHashes { get; private set; }
        public string PackagesPath { get; private set; }
        public string FilePath { get; private set; }

        public static bool TryGetGlobalSettings(string path, out GlobalSettings globalSettings)
        {
            globalSettings = null;

            string globalJsonPath = null;

            if (Path.GetFileName(path) == GlobalFileName)
            {
                globalJsonPath = path;
                path = Path.GetDirectoryName(path);
            }
            else if (!HasGlobalFile(path))
            {
                return false;
            }
            else
            {
                globalJsonPath = Path.Combine(path, GlobalFileName);
            }

            globalSettings = new GlobalSettings();

            string json = File.ReadAllText(globalJsonPath);
            var settings = JObject.Parse(json);
            var sources = settings["sources"];
            var dependencies = settings["dependencies"] as JObject;

            globalSettings.SourcePaths = sources == null ? new string[] { } : sources.ToObject<string[]>();
            globalSettings.PackagesPath = settings.Value<string>("packages");
            globalSettings.PackageHashes = new Dictionary<Library, string>();
            globalSettings.FilePath = globalJsonPath;

            if (dependencies != null)
            {
                foreach (var property in dependencies.Properties())
                {
                    var dependencyValue = dependencies[property.Name] as JObject;
                    if (dependencyValue == null)
                    {
                        throw new InvalidDataException(string.Format(
                            "The value of '{0}' in {1} must be an object", property.Name, GlobalFileName));
                    }

                    SemanticVersion version;
                    if (!SemanticVersion.TryParse(dependencyValue["version"]?.ToString(), out version))
                    {
                        throw new InvalidDataException(string.Format(
                            "The dependency '{0}' in {1} doesn't have valid version information",
                            property.Name, GlobalFileName));
                    }

                    var library = new Library()
                    {
                        Name = property.Name,
                        Version = version
                    };

                    var shaValue = dependencyValue["sha"]?.ToString();

                    if (string.IsNullOrEmpty(shaValue))
                    {
                        throw new InvalidDataException(string.Format(
                            "The dependency '{0}' in {1} doesn't have a valid SHA value",
                            property.Name, GlobalFileName));
                    }

                    globalSettings.PackageHashes[library] = shaValue;
                }
            }

            return true;
        }

        public static bool HasGlobalFile(string path)
        {
            string projectPath = Path.Combine(path, GlobalFileName);

            return File.Exists(projectPath);
        }

    }
}
