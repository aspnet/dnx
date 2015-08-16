using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Compilation;
using Microsoft.Dnx.Runtime.Loader;
using NuGet;

namespace Microsoft.Dnx.Tooling
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
        private readonly IApplicationEnvironment _appEnv;
        private readonly LibraryExporter _libraryExporter;
        private readonly IAssemblyLoadContext _defaultLoadContext;

        public BuildContext(IServiceProvider hostServices,
                            IApplicationEnvironment appEnv,
                            CompilationEngineFactory compilationFactory,
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
            _appEnv = appEnv;
            _defaultLoadContext = ((IAssemblyLoadContextAccessor)hostServices.GetService(typeof(IAssemblyLoadContextAccessor))).Default;

            _applicationHostContext = GetApplicationHostContext(project, targetFramework, compilationFactory.CompilationCache);
            var compilationEngine = compilationFactory.CreateEngine(
                new CompilationEngineContext(
                    _applicationHostContext.LibraryManager,
                    new ProjectGraphProvider(),
                    NoopWatcher.Instance,
                    _targetFramework,
                    _configuration,
                    GetBuildLoadContext(project, compilationFactory.CompilationCache)));

            _libraryExporter = compilationEngine.CreateProjectExporter(
                _project, _targetFramework, _configuration);
        }

        public void Initialize(IReport report)
        {
            ShowDependencyInformation(report);
        }

        public bool Build(List<DiagnosticMessage> diagnostics)
        {
            var export = _libraryExporter.GetExport(_project.Name);
            if (export == null)
            {
                return false;
            }

            var metadataReference = export.MetadataReferences
                .OfType<IMetadataProjectReference>()
                .FirstOrDefault(r => string.Equals(r.Name, _project.Name, StringComparison.OrdinalIgnoreCase));

            if (metadataReference == null)
            {
                return false;
            }

            var result = metadataReference.EmitAssembly(_outputPath);

            diagnostics.AddRange(_applicationHostContext.GetAllDiagnostics());

            if (result.Diagnostics != null)
            {
                diagnostics.AddRange(result.Diagnostics);
            }

            return result.Success && !diagnostics.HasErrors();
        }

        public void PopulateDependencies(PackageBuilder packageBuilder)
        {
            var dependencies = new List<PackageDependency>();
            var project = _applicationHostContext.LibraryManager.GetLibraryDescription(_project.Name);

            foreach (var dependency in project.Dependencies)
            {
                if (!dependency.HasFlag(LibraryDependencyTypeFlag.BecomesNupkgDependency))
                {
                    continue;
                }

                var dependencyDescription = _applicationHostContext.LibraryManager.GetLibraryDescription(dependency.Name);

                // REVIEW: Can we get this far with unresolved dependencies
                if (dependencyDescription == null || !dependencyDescription.Resolved)
                {
                    continue;
                }

                if (dependencyDescription.Type == LibraryTypes.Project &&
                    ((ProjectDescription)dependencyDescription).Project.EmbedInteropTypes)
                {
                    continue;
                }

                if (dependency.LibraryRange.IsGacOrFrameworkReference)
                {
                    packageBuilder.FrameworkReferences.Add(new FrameworkAssemblyReference(dependency.LibraryRange.GetReferenceAssemblyName(), new[] { _targetFramework }));
                }
                else
                {
                    IVersionSpec dependencyVersion = null;

                    if (dependency.LibraryRange.VersionRange == null ||
                        dependency.LibraryRange.VersionRange.VersionFloatBehavior != SemanticVersionFloatBehavior.None)
                    {
                        dependencyVersion = new VersionSpec
                        {
                            IsMinInclusive = true,
                            MinVersion = dependencyDescription.Identity.Version
                        };
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

            packageBuilder.DependencySets.Add(new PackageDependencySet(_targetFramework, dependencies));
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
            var projectExport = _libraryExporter.GetAllExports(_project.Name);
            if (projectExport == null)
            {
                return;
            }
            var metadataFileRefs = projectExport.MetadataReferences
                .OfType<IMetadataFileReference>();

            foreach (var library in _applicationHostContext.LibraryManager.GetLibraryDescriptions())
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

        private ApplicationHostContext GetApplicationHostContext(Runtime.Project project, FrameworkName targetFramework, CompilationCache cache)
        {
            var cacheKey = Tuple.Create("ApplicationContext", project.Name, targetFramework);

            return cache.Cache.Get<ApplicationHostContext>(cacheKey, ctx =>
            {
                var applicationHostContext = new ApplicationHostContext(projectDirectory: project.ProjectDirectory,
                                                                        packagesDirectory: null,
                                                                        targetFramework: targetFramework);

                return applicationHostContext;
            });
        }

        private IAssemblyLoadContext GetBuildLoadContext(Runtime.Project project, CompilationCache cache)
        {
            var cacheKey = Tuple.Create("RuntimeLoadContext", project.Name, _appEnv.RuntimeFramework);

            return cache.Cache.Get<IAssemblyLoadContext>(cacheKey, ctx =>
            {
                var appHostContext = GetApplicationHostContext(project, _appEnv.RuntimeFramework, cache);

                return new PackageLoadContext(appHostContext.LibraryManager, _defaultLoadContext);
            });
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