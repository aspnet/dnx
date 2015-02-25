// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime.Hosting.DependencyProviders
{
    public class GlobalSettings
    {
        public const string GlobalFileName = "global.json";

        public IList<string> ProjectPaths { get; private set; }
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

            globalSettings.ProjectPaths = sources == null ? new string[] { } : sources.ValueAsArray<string>();
            globalSettings.PackagesPath = settings.Value<string>("packages");
            globalSettings.FilePath = globalJsonPath;

            return true;
        }

        public static bool HasGlobalFile(string path)
        {
            string projectPath = Path.Combine(path, GlobalFileName);

            return File.Exists(projectPath);
        }
    }
}
