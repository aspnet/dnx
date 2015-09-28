using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Runtime;

using NuGet;

namespace Microsoft.Dnx.Tooling
{
    public class BuildContext
    {
        private readonly Runtime.Project _project;
        private readonly FrameworkName _targetFramework;
        private readonly string _targetFrameworkFolder;
        private readonly string _outputPath;
        private readonly LibraryExporter _libraryExporter;
        private ApplicationHostContext _context;
        private readonly IReport _report;

        public BuildContext(IReport report,
                            Runtime.Project project,
                            FrameworkName targetFramework,
                            LibraryExporter libraryExporter,
                            string outputPath)
        {
            _report = report;
            _project = project;
            _targetFramework = targetFramework;
            _targetFrameworkFolder = VersionUtility.GetShortFrameworkName(_targetFramework);
            _outputPath = Path.Combine(outputPath, _targetFrameworkFolder);

            _libraryExporter = libraryExporter;
        }

        public bool Build(List<DiagnosticMessage> diagnostics)
        {
            var export = _libraryExporter.ExportProject(_project, _targetFramework);

            if (export != null)
            {
                ShowDependencyInformation(export);
            }

            _context = export?.ApplicationHostContext;

            var metadataReference = export?.ProjectReference;

            if (metadataReference == null)
            {
                return false;
            }

            var result = metadataReference.EmitAssembly(_outputPath);

            diagnostics.AddRange(_context.LibraryManager.GetAllDiagnostics());

            if (result.Diagnostics != null)
            {
                diagnostics.AddRange(result.Diagnostics);
            }

            return result.Success && !diagnostics.HasErrors();
        }

        public void PopulateDependencies(PackageBuilder packageBuilder)
        {
            var dependencies = new List<PackageDependency>();
            var project = _context.MainProject;

            foreach (var dependency in project.Dependencies)
            {
                if (!dependency.HasFlag(LibraryDependencyTypeFlag.BecomesNupkgDependency))
                {
                    continue;
                }

                var dependencyDescription = _context.LibraryManager.GetLibraryDescription(dependency.Name);

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

        private void ShowDependencyInformation(ProjectExport projectExport)
        {
            var metadataFileRefs = projectExport.DependenciesExport.MetadataReferences
                .OfType<IMetadataFileReference>();

            foreach (var library in projectExport.ApplicationHostContext.LibraryManager.GetLibraryDescriptions())
            {
                if (!library.Resolved)
                {
                    _report.WriteLine("  Unable to resolve dependency {0}", library.Identity.ToString().Red().Bold());
                    _report.WriteLine();
                    continue;
                }
                _report.WriteLine("  Using {0} dependency {1}", library.Type, library.Identity);
                _report.WriteLine("    Source: {0}", HighlightFile(library.Path));

                if (library.Type == LibraryTypes.Package)
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
                        _report.WriteLine("    File: {0}", relativeAssemblyPath.Bold());
                    }
                }
                _report.WriteLine();
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