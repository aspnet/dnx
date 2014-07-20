using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.DependencyInjection;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    public class BuildContext
    {
        private readonly Project _project;
        private readonly FrameworkName _targetFramework;
        private readonly string _configuration;
        private readonly string _targetFrameworkFolder;
        private readonly string _outputPath;

        private IEnumerable<LibraryDescription> _projectDependencies;
        private IEnumerable<LibraryDescription> _resolvedDependencies;
        private IFrameworkReferenceResolver _frameworkResolver;
        private ProjectResolver _projectResolver;
        private ServiceProvider _serviceProvider;
        private ProjectBuilder _projectBuilder;

        public BuildContext(Project project, FrameworkName targetFramework, string configuration, string outputPath)
        {
            _project = project;
            _targetFramework = targetFramework;
            _configuration = configuration;
            _targetFrameworkFolder = VersionUtility.GetShortFrameworkName(_targetFramework);
            _outputPath = Path.Combine(outputPath, _targetFrameworkFolder);
            _serviceProvider = new ServiceProvider();
        }

        public IServiceProvider ServiceProvider
        {
            get
            {
                return _serviceProvider;
            }
        }

        public void Initialize()
        {
            var projectDir = _project.ProjectDirectory;
            var rootDirectory = ProjectResolver.ResolveRootDirectory(projectDir);
            _projectResolver = new ProjectResolver(projectDir, rootDirectory);
            var packagesDir = NuGetDependencyResolver.ResolveRepositoryPath(rootDirectory);

            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver();
            var nugetDependencyResolver = new NuGetDependencyResolver(packagesDir, referenceAssemblyDependencyResolver.FrameworkResolver);
            var gacDependencyResolver = new GacDependencyResolver();
            var projectReferenceResolver = new ProjectReferenceDependencyProvider(_projectResolver);
            _projectBuilder = new ProjectBuilder(_projectResolver, _serviceProvider);

            var dependencyWalker = new DependencyWalker(new IDependencyProvider[] {
                projectReferenceResolver,
                referenceAssemblyDependencyResolver,
                gacDependencyResolver,
                nugetDependencyResolver
            });

            dependencyWalker.Walk(_project.Name, _project.Version, _targetFramework);

            _projectDependencies = projectReferenceResolver.Dependencies;
            _resolvedDependencies = dependencyWalker.Libraries;
            _frameworkResolver = referenceAssemblyDependencyResolver.FrameworkResolver;

            var compositeDependencyExporter = new CompositeLibraryExportProvider(new ILibraryExportProvider[] {
                referenceAssemblyDependencyResolver,
                gacDependencyResolver,
                nugetDependencyResolver,
                _projectBuilder
            });

            _serviceProvider.Add(typeof(ILibraryExportProvider), compositeDependencyExporter);
            _serviceProvider.Add(typeof(IProjectResolver), _projectResolver);
        }

        public bool Build(IList<string> warnings, IList<string> errors)
        {
            var result = _projectBuilder.Build(_project.Name,
                                               _targetFramework,
                                               _configuration,
                                               _outputPath);

            if (result.Errors != null)
            {
                errors.AddRange(result.Errors);
            }

            if (result.Warnings != null)
            {
                warnings.AddRange(result.Warnings);
            }

            return result.Success;
        }

        public void PopulateDependencies(PackageBuilder packageBuilder)
        {
            var dependencies = new List<PackageDependency>();
            var projectReferenceByName = _projectDependencies.ToDictionary(r => r.Identity.Name);

            var frameworkAssemblies = new List<string>();

            var targetFrameworkInformation = _project.GetTargetFramework(_targetFramework);

            var targetFramework = targetFrameworkInformation.FrameworkName ?? _targetFramework;

            var projectDependencies = _project.Dependencies.Concat(targetFrameworkInformation.Dependencies)
                                                           .ToList();

            if (projectDependencies.Count > 0)
            {
                foreach (var dependency in projectDependencies.OrderBy(d => d.Name))
                {
                    Project dependencyProject;
                    if (projectReferenceByName.ContainsKey(dependency.Name) &&
                        _projectResolver.TryResolveProject(dependency.Name, out dependencyProject) &&
                        dependencyProject.EmbedInteropTypes)
                    {
                        continue;
                    }

                    string path;
                    if (_frameworkResolver.TryGetAssembly(dependency.Name, targetFramework, out path))
                    {
                        frameworkAssemblies.Add(dependency.Name);
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
                            var actual = _resolvedDependencies
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
                    packageBuilder.DependencySets.Add(new PackageDependencySet(targetFramework, dependencies));
                }
            }

            foreach (var a in frameworkAssemblies)
            {
                packageBuilder.FrameworkReferences.Add(new FrameworkAssemblyReference(a, new[] { targetFramework }));
            }
        }

        public void AddLibs(PackageBuilder packageBuilder)
        {
            // Add everything in the output folder to the lib path
            foreach (var path in Directory.EnumerateFiles(_outputPath, "*.*"))
            {
                packageBuilder.Files.Add(new PhysicalPackageFile
                {
                    SourcePath = path,
                    TargetPath = string.Format(@"lib\{0}\{1}", _targetFrameworkFolder, Path.GetFileName(path))
                });
            }
        }
    }
}