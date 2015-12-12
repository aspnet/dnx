// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Extensions.CompilationAbstractions;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class Project
    {
        public const string ProjectFileName = "project.json";

        public static readonly TypeInformation DefaultRuntimeCompiler = new TypeInformation("Microsoft.Dnx.Compilation.CSharp", "Microsoft.Dnx.Compilation.CSharp.RoslynProjectCompiler");
        public static readonly TypeInformation DefaultDesignTimeCompiler = new TypeInformation("Microsoft.Dnx.Compilation.DesignTime", "Microsoft.Dnx.Compilation.DesignTime.DesignTimeHostProjectCompiler");
        public static int DesignTimeCompilerPort;

        public static TypeInformation DefaultCompiler = DefaultRuntimeCompiler;

        // REVIEW: It's kinda hacky making these internal but the reader needs to set them
        internal Dictionary<FrameworkName, TargetFrameworkInformation> _targetFrameworks = new Dictionary<FrameworkName, TargetFrameworkInformation>();
        internal Dictionary<FrameworkName, CompilerOptions> _compilerOptionsByFramework = new Dictionary<FrameworkName, CompilerOptions>();
        internal Dictionary<string, CompilerOptions> _compilerOptionsByConfiguration = new Dictionary<string, CompilerOptions>(StringComparer.OrdinalIgnoreCase);

        internal CompilerOptions _defaultCompilerOptions;
        internal TargetFrameworkInformation _defaultTargetFrameworkConfiguration;

        public Project()
        {
        }

        public string ProjectFilePath { get; set; }

        public string ProjectDirectory
        {
            get
            {
                return Path.GetDirectoryName(ProjectFilePath);
            }
        }

        public string Name { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Copyright { get; set; }

        public string Summary { get; set; }

        public string Language { get; set; }

        public string ReleaseNotes { get; set; }

        public string[] Authors { get; set; }

        public string[] Owners { get; set; }

        public bool EmbedInteropTypes { get; set; }

        public SemanticVersion Version { get; set; }

        public Version AssemblyFileVersion { get; set; }

        public IList<LibraryDependency> Dependencies { get; set; }

        public CompilerServices CompilerServices { get; set; }

        public string EntryPoint { get; set; }

        public string ProjectUrl { get; set; }

        public string LicenseUrl { get; set; }

        public string IconUrl { get; set; }

        public bool RequireLicenseAcceptance { get; set; }

        public string[] Tags { get; set; }

        public bool IsLoadable { get; set; }

        public ProjectFilesCollection Files { get; set; }

        public IDictionary<string, string> Commands { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, IEnumerable<string>> Scripts { get; } = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<TargetFrameworkInformation> GetTargetFrameworks()
        {
            return _targetFrameworks.Values;
        }

        public IEnumerable<string> GetConfigurations()
        {
            return _compilerOptionsByConfiguration.Keys;
        }

        public static bool HasProjectFile(string path)
        {
            string projectPath = Path.Combine(path, ProjectFileName);

            return File.Exists(projectPath);
        }

        public static bool TryGetProject(string path, out Project project, ICollection<DiagnosticMessage> diagnostics = null)
        {
            project = null;

            string projectPath = null;

            if (string.Equals(Path.GetFileName(path), ProjectFileName, StringComparison.OrdinalIgnoreCase))
            {
                projectPath = path;
                path = Path.GetDirectoryName(path);
            }
            else if (!HasProjectFile(path))
            {
                return false;
            }
            else
            {
                projectPath = Path.Combine(path, ProjectFileName);
            }

            // Assume the directory name is the project name if none was specified
            var projectName = PathUtility.GetDirectoryName(path);
            projectPath = Path.GetFullPath(projectPath);

            if (!File.Exists(projectPath))
            {
                return false;
            }

            try
            {
                using (var stream = File.OpenRead(projectPath))
                {
                    var reader = new ProjectReader();
                    project = reader.ReadProject(stream, projectName, projectPath, diagnostics);
                }
            }
            catch (Exception ex)
            {
                throw FileFormatException.Create(ex, projectPath);
            }

            return true;
        }

        public ICompilerOptions GetCompilerOptions(FrameworkName targetFramework,
                                                   string configurationName)
        {
            // Get all project options and combine them
            var rootOptions = GetCompilerOptions();
            var configurationOptions = configurationName != null ? GetCompilerOptions(configurationName) : null;
            var targetFrameworkOptions = targetFramework != null ? GetCompilerOptions(targetFramework) : null;

            // Combine all of the options
            return CompilerOptions.Combine(rootOptions, configurationOptions, targetFrameworkOptions);
        }

        public TargetFrameworkInformation GetTargetFramework(FrameworkName targetFramework)
        {
            TargetFrameworkInformation targetFrameworkInfo = null;
            if (targetFramework != null && _targetFrameworks.TryGetValue(targetFramework, out targetFrameworkInfo))
            {
                return targetFrameworkInfo;
            }

            return targetFrameworkInfo ?? _defaultTargetFrameworkConfiguration;
        }

        public CompilerOptions GetCompilerOptions()
        {
            return _defaultCompilerOptions;
        }

        public CompilerOptions GetCompilerOptions(string configurationName)
        {
            CompilerOptions options;
            if (_compilerOptionsByConfiguration.TryGetValue(configurationName, out options))
            {
                return options;
            }

            return null;
        }

        public CompilerOptions GetCompilerOptions(FrameworkName frameworkName)
        {
            CompilerOptions options;
            if (_compilerOptionsByFramework.TryGetValue(frameworkName, out options))
            {
                return options;
            }

            return null;
        }
    }
}
