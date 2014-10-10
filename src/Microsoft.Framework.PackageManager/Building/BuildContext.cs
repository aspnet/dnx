using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    public class BuildContext
    {
        private readonly Runtime.Project _project;
        private readonly FrameworkName _targetFramework;
        private readonly string _configuration;
        private readonly string _targetFrameworkFolder;
        private readonly string _outputPath;
        private readonly ApplicationHostContext _applicationHostContext;

        public BuildContext(ICache cache, ICacheContextAccessor cacheContextAccessor, Runtime.Project project, FrameworkName targetFramework, string configuration, string outputPath)
        {
            _project = project;
            _targetFramework = targetFramework;
            _configuration = configuration;
            _targetFrameworkFolder = VersionUtility.GetShortFrameworkName(_targetFramework);
            _outputPath = Path.Combine(outputPath, _targetFrameworkFolder);
            _applicationHostContext = new ApplicationHostContext(
                serviceProvider: null,
                projectDirectory: project.ProjectDirectory,
                packagesDirectory: null,
                configuration: configuration,
                targetFramework: targetFramework,
                cache: cache,
                cacheContextAccessor: cacheContextAccessor,
                namedCacheDependencyProvider: new NamedCacheDependencyProvider());
        }

        public void Initialize(IReport report)
        {
            _applicationHostContext.DependencyWalker.Walk(_project.Name, _project.Version, _targetFramework);
            ShowDependencyInformation(report);
        }

        public bool Build(IList<string> warnings, IList<string> errors)
        {
            var builder = _applicationHostContext.CreateInstance<ProjectBuilder>();

            var result = builder.Build(_project.Name, _outputPath);

            if (result.Errors != null)
            {
                errors.AddRange(result.Errors);
            }

            if (result.Warnings != null)
            {
                warnings.AddRange(result.Warnings);
            }

            return result.Success && errors.Count == 0;
        }

        public void PopulateDependencies(PackageBuilder packageBuilder)
        {
            var dependencies = new List<PackageDependency>();
            var projectReferenceByName = _applicationHostContext.ProjectDepencyProvider
                                                                .Dependencies
                                                                .ToDictionary(r => r.Identity.Name);

            LibraryDescription description;
            if (!projectReferenceByName.TryGetValue(_project.Name, out description))
            {
                return;
            }

            foreach (var dependency in description.Dependencies)
            {
                if (!dependency.HasFlag(LibraryDependencyTypeFlag.BecomesNupkgDependency))
                {
                    continue;
                }

                Runtime.Project dependencyProject;
                if (projectReferenceByName.ContainsKey(dependency.Name) &&
                    _applicationHostContext.ProjectResolver.TryResolveProject(dependency.Name, out dependencyProject) &&
                    dependencyProject.EmbedInteropTypes)
                {
                    continue;
                }

                if (dependency.Library.IsGacOrFrameworkReference)
                {
                    packageBuilder.FrameworkReferences.Add(new FrameworkAssemblyReference(dependency.Name, new[] { _targetFramework }));
                }
                else
                {
                    var dependencyVersion = new VersionSpec()
                    {
                        IsMinInclusive = true,
                        MinVersion = dependency.Version
                    };

                    if (dependencyVersion.MinVersion == null || dependencyVersion.MinVersion.IsSnapshot)
                    {
                        var actual = _applicationHostContext.DependencyWalker.Libraries
                            .Where(pkg => string.Equals(pkg.Identity.Name, _project.Name, StringComparison.OrdinalIgnoreCase))
                            .SelectMany(pkg => pkg.Dependencies)
                            .SingleOrDefault(dep => string.Equals(dep.Name, dependency.Name, StringComparison.OrdinalIgnoreCase));

                        if (actual != null)
                        {
                            dependencyVersion.MinVersion = actual.Version;
                        }
                    }

                    dependencies.Add(new PackageDependency(dependency.Name, dependencyVersion));
                }
            }

            if (dependencies.Count > 0)
            {
                packageBuilder.DependencySets.Add(new PackageDependencySet(_targetFramework, dependencies));
            }
        }

        public void AddLibs(PackageBuilder packageBuilder, string pattern)
        {
            // Add everything in the output folder to the lib path
            foreach (var path in Directory.EnumerateFiles(_outputPath, pattern))
            {
                packageBuilder.Files.Add(new PhysicalPackageFile
                {
                    SourcePath = path,
                    TargetPath = Path.Combine("lib", _targetFrameworkFolder, Path.GetFileName(path))
                });
            }
        }

        private void ShowDependencyInformation(IReport report)
        {
            // Make lookup for actual package dependency assemblies
            var projectExport = _applicationHostContext.LibraryManager.GetAllExports(_project.Name);
            if (projectExport == null)
            {
                return;
            }
            var metadataFileRefs = projectExport.MetadataReferences
                .OfType<IMetadataFileReference>();

            foreach (var library in _applicationHostContext.DependencyWalker.Libraries)
            {
                var libraryPath = NormalizeDirectoryPath(library.Path);
                report.WriteLine("Using {0} dependency {1} for {2}", library.Type, library.Identity, _targetFramework);
                report.WriteLine("  Source: {0}", libraryPath);

                if (library.Type == "Package")
                {
                    // TODO: temporarily use prefix to tell whether an assembly belongs to a package
                    // Should expose LibraryName from IMetadataReference later for more efficient lookup
                    var packageAssemblies = metadataFileRefs.Where(x => Path.GetFullPath(x.Path).StartsWith(libraryPath));
                    foreach (var assembly in packageAssemblies)
                    {
                        var relativeAssemblyPath = PathUtility.GetRelativePath(
                            libraryPath,
                            Path.GetFullPath(assembly.Path));
                        report.WriteLine("  File: {0}", relativeAssemblyPath);
                    }
                }
            }
        }

        private static string NormalizeDirectoryPath(string path)
        {
            return PathUtility.EnsureTrailingSlash(Path.GetFullPath(path));
        }
    }
}