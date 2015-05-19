using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Caching;
using Microsoft.Framework.Runtime.Compilation;
using Microsoft.Framework.Runtime.Loader;
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

        private readonly IServiceProvider _hostServices;
        private readonly ICache _cache;
        private readonly ICacheContextAccessor _cacheContextAccessor;
        private readonly IApplicationEnvironment _appEnv;

        public BuildContext(IServiceProvider hostServices,
                            IApplicationEnvironment appEnv,
                            ICache cache,
                            ICacheContextAccessor cacheContextAccessor,
                            Runtime.Project project,
                            FrameworkName targetFramework,
                            string configuration,
                            string outputPath)
        {
            _project = project;
            _targetFramework = targetFramework;
            _configuration = configuration;
            _targetFrameworkFolder = VersionUtility.GetShortFrameworkName(_targetFramework);
            _outputPath = Path.Combine(outputPath, _targetFrameworkFolder);
            _hostServices = hostServices;
            _cache = cache;
            _cacheContextAccessor = cacheContextAccessor;
            _appEnv = appEnv;

            _applicationHostContext = GetApplicationHostContext(project, targetFramework, configuration);
        }

        public void Initialize(IReport report)
        {
            ShowDependencyInformation(report);
        }

        public bool Build(List<ICompilationMessage> diagnostics)
        {
            var builder = _applicationHostContext.CreateInstance<ProjectBuilder>();

            var result = builder.Build(_project.Name, _outputPath);

            var errors = _applicationHostContext.DependencyWalker.GetDependencyDiagnostics(_project.ProjectFilePath);
            diagnostics.AddRange(errors);

            if (result.Diagnostics != null)
            {
                diagnostics.AddRange(result.Diagnostics);
            }

            return result.Success && !diagnostics.HasErrors();
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

                if (dependency.LibraryRange.IsGacOrFrameworkReference)
                {
                    packageBuilder.FrameworkReferences.Add(new FrameworkAssemblyReference(dependency.Name, new[] { _targetFramework }));
                }
                else
                {
                    IVersionSpec dependencyVersion = null;

                    if (dependency.LibraryRange.VersionRange == null ||
                        dependency.LibraryRange.VersionRange.VersionFloatBehavior != SemanticVersionFloatBehavior.None)
                    {
                        var actual = _applicationHostContext.DependencyWalker.Libraries
                            .Where(pkg => string.Equals(pkg.Identity.Name, _project.Name, StringComparison.OrdinalIgnoreCase))
                            .SelectMany(pkg => pkg.Dependencies)
                            .SingleOrDefault(dep => string.Equals(dep.Name, dependency.Name, StringComparison.OrdinalIgnoreCase));

                        if (actual != null)
                        {
                            dependencyVersion = new VersionSpec
                            {
                                IsMinInclusive = true,
                                MinVersion = actual.Library.Version
                            };
                        }
                    }
                    else
                    {
                        var versionRange = dependency.LibraryRange.VersionRange;

                        dependencyVersion = new VersionSpec
                        {
                            IsMinInclusive = true,
                            MinVersion = versionRange.MinVersion,
                            MaxVersion = versionRange.MaxVersion,
                            IsMaxInclusive = versionRange.IsMaxInclusive
                        };
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
                if (string.IsNullOrEmpty(library.Path))
                {
                    report.WriteLine("  Unable to resolve dependency {0}", library.Identity.ToString().Red().Bold());
                    report.WriteLine();
                    continue;
                }
                report.WriteLine("  Using {0} dependency {1}", library.Type, library.Identity);
                report.WriteLine("    Source: {0}", HighlightFile(library.Path));

                if (library.Type == "Package")
                {
                    // TODO: temporarily use prefix to tell whether an assembly belongs to a package
                    // Should expose LibraryName from IMetadataReference later for more efficient lookup
                    var libraryPath = NormalizeDirectoryPath(library.Path);
                    var packageAssemblies = metadataFileRefs.Where(x => Path.GetFullPath(x.Path).StartsWith(libraryPath));
                    foreach (var assembly in packageAssemblies)
                    {
                        var relativeAssemblyPath = PathUtility.GetRelativePath(
                            libraryPath,
                            Path.GetFullPath(assembly.Path));
                        report.WriteLine("    File: {0}", relativeAssemblyPath.Bold());
                    }
                }
                report.WriteLine();
            }
        }

        private ApplicationHostContext GetApplicationHostContext(Runtime.Project project, FrameworkName targetFramework, string configuration)
        {
            var cacheKey = Tuple.Create("ApplicationContext", project.Name, configuration, targetFramework);

            return _cache.Get<ApplicationHostContext>(cacheKey, ctx =>
            {
                var applicationHostContext = new ApplicationHostContext(serviceProvider: _hostServices,
                                                                        projectDirectory: project.ProjectDirectory,
                                                                        packagesDirectory: null,
                                                                        configuration: configuration,
                                                                        targetFramework: targetFramework,
                                                                        cache: _cache,
                                                                        cacheContextAccessor: _cacheContextAccessor,
                                                                        namedCacheDependencyProvider: null,
                                                                        loadContextFactory: GetRuntimeLoadContextFactory(project));

                applicationHostContext.DependencyWalker.Walk(project.Name, project.Version, targetFramework);

                return applicationHostContext;
            });
        }

        private IAssemblyLoadContextFactory GetRuntimeLoadContextFactory(Runtime.Project project)
        {
            var applicationHostContext = new ApplicationHostContext(_hostServices,
                                                                    project.ProjectDirectory,
                                                                    packagesDirectory: null,
                                                                    configuration: _appEnv.Configuration,
                                                                    targetFramework: _appEnv.RuntimeFramework,
                                                                    cache: _cache,
                                                                    cacheContextAccessor: _cacheContextAccessor,
                                                                    namedCacheDependencyProvider: null,
                                                                    loadContextFactory: null);

            applicationHostContext.DependencyWalker.Walk(project.Name, project.Version, _appEnv.RuntimeFramework);

            return new AssemblyLoadContextFactory(applicationHostContext.ServiceProvider);
        }

        private static string NormalizeDirectoryPath(string path)
        {
            return PathUtility.EnsureTrailingSlash(Path.GetFullPath(path));
        }

        private static string HighlightFile(string path)
        {
            return File.Exists(path) ? path.Bold() : path;
        }
    }
}