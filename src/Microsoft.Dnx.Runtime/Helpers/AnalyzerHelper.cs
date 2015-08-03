// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;
using NuGet;

namespace Microsoft.Dnx.Runtime.Helpers
{
    internal static class AnalyzerHelper
    {
        private static readonly string AnalyzerBaseFolder = "analyzers";
        private static readonly string AnalyzerLanguage = "cs";
        private static readonly string AnalyzerExtension = ".dll";

        public static IEnumerable<IAnalyzerReference> GetAnalyerReferences(PackageInfo packageInfo,
                                                                           string packagePath,
                                                                           FrameworkName runtimeFramework)
        {
            var files = packageInfo.LockFileLibrary.Files.Select(CreateFileReference)
                                                         .Where(reference => reference != null);

            IEnumerable<IFrameworkTargetable> compatible;
            VersionUtility.GetNearest(runtimeFramework, files.OfType<IFrameworkTargetable>(), out compatible);

            if (compatible?.Any() ?? false)
            {
                yield return new AnalyzerReference(
                    packageInfo.Id,
                    compatible.OfType<FileReference>().Select(reference => Path.Combine(packagePath, reference.RelativePath)));
            }
        }

        private static FileReference CreateFileReference(string relativePath)
        {
            if (!relativePath.StartsWith(AnalyzerBaseFolder, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!relativePath.EndsWith(AnalyzerExtension, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // assumption: analyzers' paths are in following format
            // analyzer/{framework}/{cs|vb}/{filename}.dll
            var parts = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            if (parts.Length != 4)
            {
                return null;
            }

            if (!string.Equals(parts[2], AnalyzerLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var framework = VersionUtility.ParseFrameworkFolderName(parts[1]);

            return new FileReference(relativePath, framework);
        }

        private class AnalyzerReference : IAnalyzerReference
        {
            public AnalyzerReference(string name, IEnumerable<string> files)
            {
                Name = name;
                Files = files;
            }

            public string Name { get; }

            public IEnumerable<string> Files { get; }
        }

        private class FileReference : IFrameworkTargetable
        {
            public FileReference(string path, FrameworkName framework)
            {
                SupportedFrameworks = new[] { framework };
                RelativePath = path;
            }

            public IEnumerable<FrameworkName> SupportedFrameworks { get; }

            public string RelativePath { get; }
        }
    }
}
