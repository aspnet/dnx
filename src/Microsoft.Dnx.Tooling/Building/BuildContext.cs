using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.CompilationAbstractions;

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
        private readonly LibraryManager _libraryManager;
        private readonly LibraryExporter _libraryExporter;

        public BuildContext(CompilationEngine compilationEngine,
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

            _libraryExporter = compilationEngine.CreateProjectExporter(
                _project, _targetFramework, _configuration);

            _libraryManager = _libraryExporter.LibraryManager;
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

            diagnostics.AddRange(_libraryManager.GetAllDiagnostics());

            if (result.Diagnostics != null)
            {
                diagnostics.AddRange(result.Diagnostics);
            }

            return result.Success && !diagnostics.HasErrors();
        }

        public void PopulateDependencies(PackageBuilder packageBuilder)
        {
            var dependencies = new List<PackageDependency>();
            var project = _libraryManager.GetLibraryDescription(_project.Name);

            foreach (var dependency in project.Dependencies)
            {
                if (!dependency.HasFlag(LibraryDependencyTypeFlag.BecomesNupkgDependency))
                {
                    continue;
                }

                var dependencyDescription = _libraryManager.GetLibraryDescription(dependency.Name);

                // REVIEW: Can we get this far with unresolved dependencies
                if (dependencyDescription == null || !dependencyDescription.Resolved)
                {
                    continue;
                }

                if (dependencyDescription.Type == Runtime.LibraryTypes.Project &&
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
            AddLibs(packageBuilder, pattern, recursiveSearch: false);
        }

        public void AddLibs(PackageBuilder packageBuilder, string pattern, bool recursiveSearch)
        {
            // Look for .dll,.xml files in top directory
            var searchOption = SearchOption.TopDirectoryOnly;
            if (recursiveSearch)
            {
                //for .resources.dll, search all directories
                searchOption = SearchOption.AllDirectories;
            }
            // Add everything in the output folder to the lib path
            foreach (var path in Directory.EnumerateFiles(_outputPath, pattern, searchOption))
            {
                var targetPath = Path.Combine("lib", _targetFrameworkFolder, Path.GetFileName(path));
                if (!Path.GetDirectoryName(path).Equals(_outputPath))
                {
                    string folderName = PathUtility.GetDirectoryName(Path.GetDirectoryName(path));
                    targetPath = Path.Combine("lib", _targetFrameworkFolder, folderName, Path.GetFileName(path));
                }

                packageBuilder.Files.Add(new PhysicalPackageFile
                {
                    SourcePath = path,
                    TargetPath = targetPath
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

            foreach (var library in _libraryManager.GetLibraryDescriptions())
            {
                if (!library.Resolved)
                {
                    report.WriteLine("  Unable to resolve dependency {0}", library.Identity.ToString().Red().Bold());
                    report.WriteLine();
                    continue;
                }
                report.WriteLine("  Using {0} dependency {1}", library.Type, library.Identity);
                report.WriteLine("    Source: {0}", HighlightFile(library.Path));

                if (library.Type == Runtime.LibraryTypes.Package)
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