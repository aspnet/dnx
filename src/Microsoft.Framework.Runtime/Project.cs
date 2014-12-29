// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class Project
    {
        public const string ProjectFileName = "project.json";

        private readonly Dictionary<FrameworkName, TargetFrameworkInformation> _targetFrameworks
            = new Dictionary<FrameworkName, TargetFrameworkInformation>();
        private readonly TargetFrameworkInformation _defaultTargetFrameworkConfiguration = new TargetFrameworkInformation
        {
            Dependencies = new List<LibraryDependency>()
        };

        public string ProjectFilePath { get; set; }

        public string ProjectDirectory
        {
            get
            {
                return Path.GetDirectoryName(ProjectFilePath);
            }
        }

        public string Name { get; set; }

        public string Description { get; set; }

        public string[] Authors { get; set; }

        public bool EmbedInteropTypes { get; set; }

        public SemanticVersion Version { get; set; }

        public IList<LibraryDependency> Dependencies { get; }
            = new List<LibraryDependency>();

        public LanguageServices LanguageServices { get; set; }

        public string WebRoot { get; set; }

        public string EntryPoint { get; set; }

        public string ProjectUrl { get; set; }

        public bool RequireLicenseAcceptance { get; set; }

        public bool IsLoadable { get; set; }

        public string[] Tags { get; set; }

        public IDictionary<string, string> Commands { get; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, IEnumerable<string>> Scripts { get; }
            = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> SourcePatterns { get; set; }

        public IEnumerable<string> ExcludePatterns { get; set; }

        public IEnumerable<string> PackExcludePatterns { get; set; }

        public IEnumerable<string> PreprocessPatterns { get; set; }

        public IEnumerable<string> SharedPatterns { get; set; }

        public IEnumerable<string> ResourcesPatterns { get; set; }

        public IEnumerable<string> ContentsPatterns { get; set; }

        public ICompilerOptions DefaultCompilerOptions { get; set; }

        public IDictionary<string, ICompilerOptions> Configurations { get; }
            = new Dictionary<string, ICompilerOptions>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> SourceFiles
        {
            get
            {
                string path = ProjectDirectory;

                var files = Enumerable.Empty<string>();

                var includeFiles = SourcePatterns
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                var excludePatterns = PreprocessPatterns.Concat(SharedPatterns).Concat(ResourcesPatterns).Concat(ExcludePatterns)
                    .Select(pattern => PathResolver.NormalizeWildcardForExcludedFiles(path, pattern))
                    .ToArray();

                var excludeFiles = PathResolver.GetMatches(
                    includeFiles,
                    x => x,
                    excludePatterns.Select(x => Path.Combine(path, x)))
                    .ToArray();

                return files.Concat(includeFiles.Except(excludeFiles)).Distinct().ToArray();
            }
        }

        public IEnumerable<string> PreprocessSourceFiles
        {
            get
            {
                string path = ProjectDirectory;

                var files = Enumerable.Empty<string>();

                var includeFiles = PreprocessPatterns
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                var excludePatterns = SharedPatterns.Concat(ResourcesPatterns).Concat(ExcludePatterns)
                    .Select(pattern => PathResolver.NormalizeWildcardForExcludedFiles(path, pattern))
                    .ToArray();

                var excludeFiles = PathResolver.GetMatches(
                    includeFiles,
                    x => x,
                    excludePatterns.Select(x => Path.Combine(path, x)))
                    .ToArray();

                return files.Concat(includeFiles.Except(excludeFiles)).Distinct().ToArray();
            }
        }

        public IEnumerable<string> PackExcludeFiles
        {
            get
            {
                string path = ProjectDirectory;

                var packExcludeFiles = PackExcludePatterns
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                return packExcludeFiles;
            }
        }

        public IEnumerable<string> ResourceFiles
        {
            get
            {
                string path = ProjectDirectory;

                var includeFiles = ResourcesPatterns
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                return includeFiles;
            }
        }

        public IEnumerable<string> SharedFiles
        {
            get
            {
                string path = ProjectDirectory;

                var includeFiles = SharedPatterns
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                return includeFiles;
            }
        }

        public IEnumerable<string> ContentFiles
        {
            get
            {
                string path = ProjectDirectory;

                var includeFiles = ContentsPatterns
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                var excludePatterns = PreprocessPatterns.Concat(SharedPatterns).Concat(ResourcesPatterns)
                    .Concat(PackExcludePatterns).Concat(SourcePatterns)
                    .Select(pattern => PathResolver.NormalizeWildcardForExcludedFiles(path, pattern))
                    .ToArray();

                var excludeFiles = PathResolver.GetMatches(
                    includeFiles,
                    x => x,
                    excludePatterns.Select(x => Path.Combine(path, x)))
                    .ToArray();

                return includeFiles.Except(excludeFiles).Distinct().ToArray();
            }
        }

        public string DefaultLanguageServicesAssembly { get; set; }
            = "Microsoft.Framework.Runtime.Roslyn";

        public string DefaultProjectReferenceProviderType { get; set; }
            = "Microsoft.Framework.Runtime.Roslyn.RoslynProjectReferenceProvider";

        public string DefaultCompilationOptionsReaderType { get; set; }
           = "Microsoft.Framework.Runtime.Roslyn.RoslynCompilerOptionsReader";

        public void AddTargetFramework(FrameworkName targetFramework, TargetFrameworkInformation targetFrameworkInformation)
        {
            _targetFrameworks[targetFramework] = targetFrameworkInformation;
        }

        public IEnumerable<TargetFrameworkInformation> GetTargetFrameworks()
        {
            return _targetFrameworks.Values;
        }

        public TargetFrameworkInformation GetTargetFramework(FrameworkName targetFramework)
        {
            TargetFrameworkInformation targetFrameworkInfo;
            if (_targetFrameworks.TryGetValue(targetFramework, out targetFrameworkInfo))
            {
                return targetFrameworkInfo;
            }

            IEnumerable<TargetFrameworkInformation> compatibleConfigurations;
            if (VersionUtility.TryGetCompatibleItems(targetFramework, GetTargetFrameworks(), out compatibleConfigurations) &&
                compatibleConfigurations.Any())
            {
                targetFrameworkInfo = compatibleConfigurations.FirstOrDefault();
            }

            return targetFrameworkInfo ?? _defaultTargetFrameworkConfiguration;
        }

        public IEnumerable<string> GetConfigurations()
        {
            return Configurations.Keys;
        }

        public ICompilerOptions GetCompilerOptions()
        {
            return DefaultCompilerOptions;
        }

        public ICompilerOptions GetCompilerOptions(string configurationName)
        {
            ICompilerOptions options;
            if (Configurations.TryGetValue(configurationName, out options))
            {
                return options;
            }

            return null;
        }

        public ICompilerOptions GetCompilerOptions(FrameworkName frameworkName)
        {
            TargetFrameworkInformation info;
            if (_targetFrameworks.TryGetValue(frameworkName, out info))
            {
                return info.CompilerOptions;
            }

            return null;
        }
    }
}
