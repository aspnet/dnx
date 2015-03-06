// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Microsoft.Framework.Runtime
{
    public class Project
    {
        public Project(PackageSpec packageSpec)
        {
            BaseDirectory = Path.GetDirectoryName(packageSpec.FilePath);
            Metadata = packageSpec;
            Files = new ProjectFilesCollection(packageSpec.Properties, packageSpec.BaseDirectory, packageSpec.FilePath);

            // Load additional metadata from the project json
            EntryPoint = Metadata.Properties.GetValue<string>("entryPoint");

            var commands = Metadata.Properties["commands"] as JObject;
            if (commands != null)
            {
                foreach (var command in commands)
                {
                    Commands[command.Key] = command.Value.Value<string>();
                }
            }
        }

        public string BaseDirectory { get; }
        public string Name { get { return Metadata.Name; } }
        public string FilePath { get { return Metadata.FilePath; } }
        public NuGetVersion Version { get { return Metadata.Version; } }
        public ProjectFilesCollection Files { get; }
        public PackageSpec Metadata { get; }
        public string EntryPoint { get; }
        public IDictionary<string, string> Commands { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}