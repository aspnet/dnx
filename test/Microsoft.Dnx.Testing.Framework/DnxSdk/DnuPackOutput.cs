// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Dnx.Testing.Framework
{
    public class DnuPackOutput : ExecResult
    {
        public DnuPackOutput(string outputPath, string packageName, string configuration)
        {
            RootPath = outputPath;
            PackageName = packageName;
            Configuration = configuration;
            var basePath = Path.Combine(RootPath, Configuration);
            PackagePath = Directory.Exists(basePath) ? Directory.GetFiles(basePath, $"*{Constants.PackageExtension}")
                .Where(x => Path.GetFileName(x).StartsWith(packageName))
                .FirstOrDefault(x => !x.EndsWith($"*.symbols{Constants.PackageExtension}"))
                : null;
        }

        public string RootPath { get; private set; }

        public string Configuration { get; private set; }

        public string PackageName { get; private set; }

        public string PackagePath { get; private set; }

        public string GetAssemblyPath(FrameworkName framework)
        {
            var shortName = VersionUtility.GetShortFrameworkName(framework);
            return Path.Combine(RootPath, Configuration, shortName, $"{PackageName}.dll");
        }
    }
}
