using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using NuGet;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class RoslynArtifactsProducer
    {
        private readonly IRoslynCompiler _compiler;
        private readonly IResourceProvider _resourceProvider;
        private readonly IGlobalAssemblyCache _globalAssemblyCache;
        private readonly IEnumerable<LibraryDescription> _resolvedDependencies;

        public RoslynArtifactsProducer(IRoslynCompiler compiler,
                                       IResourceProvider resourceProvider,
                                       IGlobalAssemblyCache globalAssemblyCache,
                                       IEnumerable<LibraryDescription> resolvedDependencies)
        {
            _compiler = compiler;
            _resourceProvider = resourceProvider;
            _globalAssemblyCache = globalAssemblyCache;
            _resolvedDependencies = resolvedDependencies;
        }

        public bool Build(BuildContext buildContext, List<Diagnostic> diagnostics)
        {
            var compilationContext = _compiler.CompileProject(buildContext.AssemblyName, buildContext.TargetFramework);

            if (compilationContext == null)
            {
                return false;
            }

            buildContext.CompilationContext = compilationContext;

            var project = compilationContext.Project;
            var name = project.Name;

            var resources = _resourceProvider.GetResources(project);

            resources.AddEmbeddedReferences(compilationContext.GetRequiredEmbeddedReferences());

            diagnostics.AddRange(compilationContext.Diagnostics);

            // If the output path is null then load the assembly from memory
            var assemblyPath = Path.Combine(buildContext.OutputPath, name + ".dll");
            var pdbPath = Path.Combine(buildContext.OutputPath, name + ".pdb");

            if (CompileToDisk(buildContext, assemblyPath, pdbPath, compilationContext, resources, diagnostics))
            {
                // Build packages for this project
                BuildPackages(buildContext, compilationContext, assemblyPath, pdbPath);
                return true;
            }

            return false;
        }

        private void BuildPackages(BuildContext buildContext, CompilationContext compilationContext, string assemblyPath, string pdbPath)
        {
            // Build packages
            if (buildContext.PackageBuilder != null)
            {
                BuildPackage(buildContext, compilationContext, assemblyPath);
            }

            if (buildContext.SymbolPackageBuilder != null)
            {
                BuildSymbolsPackage(buildContext, compilationContext, assemblyPath, pdbPath);
            }
        }

        private void BuildSymbolsPackage(BuildContext buildContext, CompilationContext compilationContext, string assemblyPath, string pdbPath)
        {
            var framework = buildContext.TargetFramework;
            var project = compilationContext.Project;
            var frameworkFolder = VersionUtility.GetShortFrameworkName(framework);
            var root = project.ProjectDirectory;

            // REVIEW: How do we handle meta programming scenarios
            foreach (var syntaxTree in compilationContext.Compilation.SyntaxTrees)
            {
                if (String.IsNullOrEmpty(syntaxTree.FilePath))
                {
                    continue;
                }

                var srcFile = new PhysicalPackageFile();
                srcFile.SourcePath = syntaxTree.FilePath;
                srcFile.TargetPath = Path.Combine("src", PathUtility.GetRelativePath(root, syntaxTree.FilePath));
                buildContext.SymbolPackageBuilder.Files.Add(srcFile);
            }

            var assemblyFile = new PhysicalPackageFile();
            assemblyFile.SourcePath = assemblyPath;
            assemblyFile.TargetPath = String.Format(@"lib\{0}\{1}.dll", frameworkFolder, project.Name);
            buildContext.SymbolPackageBuilder.Files.Add(assemblyFile);

            var pdbFile = new PhysicalPackageFile();
            pdbFile.SourcePath = pdbPath;
            pdbFile.TargetPath = String.Format(@"lib\{0}\{1}.pdb", frameworkFolder, project.Name);
            buildContext.SymbolPackageBuilder.Files.Add(pdbFile);
        }

        private void BuildPackage(BuildContext buildContext, CompilationContext compilationContext, string assemblyPath)
        {
            var targetFramework = buildContext.TargetFramework;
            var dependencies = new List<PackageDependency>();
            var project = compilationContext.Project;
            var projectReferenceByName = compilationContext.MetadataReferences.OfType<RoslynProjectReference>()
                                                                              .Select(r => r.CompliationContext)
                                                                              .ToDictionary(p => p.Project.Name, p => p.Project);
            var frameworkAssemblies = new List<string>();

            var targetFrameworkConfig = project.GetTargetFrameworkConfiguration(targetFramework);

            var projectDependencies = project.Dependencies.Concat(targetFrameworkConfig.Dependencies)
                                                          .ToList();

            if (projectDependencies.Count > 0)
            {
                foreach (var dependency in projectDependencies.OrderBy(d => d.Name))
                {
                    Project dependencyProject;
                    if (projectReferenceByName.TryGetValue(dependency.Name, out dependencyProject) && dependencyProject.EmbedInteropTypes)
                    {
                        continue;
                    }

                    if (VersionUtility.IsDesktop(targetFramework) &&
                        _globalAssemblyCache.IsInGac(dependency.Name))
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
                                .Where(pkg => string.Equals(pkg.Identity.Name, project.Name, StringComparison.OrdinalIgnoreCase))
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
                    buildContext.PackageBuilder.DependencySets.Add(new PackageDependencySet(targetFramework, dependencies));
                }
            }

            // Only do this on full desktop
            if (VersionUtility.IsDesktop(buildContext.TargetFramework))
            {
                foreach (var a in frameworkAssemblies)
                {
                    buildContext.PackageBuilder.FrameworkReferences.Add(new FrameworkAssemblyReference(a));
                }
            }

            var file = new PhysicalPackageFile();
            file.SourcePath = assemblyPath;
            var folder = VersionUtility.GetShortFrameworkName(targetFramework);
            file.TargetPath = String.Format(@"lib\{0}\{1}.dll", folder, project.Name);
            buildContext.PackageBuilder.Files.Add(file);
        }

        private bool CompileToDisk(BuildContext buildContext, string assemblyPath, string pdbPath, CompilationContext compilationContext, IList<ResourceDescription> resources, List<Diagnostic> diagnostics)
        {
            // REVIEW: Memory bloat?
            using (var pdbStream = new MemoryStream())
            using (var assemblyStream = new MemoryStream())
            {
                Trace.TraceInformation("[{0}]: Emitting assembly for {1}", GetType().Name, buildContext.AssemblyName);

                var sw = Stopwatch.StartNew();

                EmitResult result = compilationContext.Compilation.Emit(assemblyStream, outputName: Path.GetFileName(assemblyPath), pdbFileName: pdbPath, pdbStream: pdbStream, manifestResources: resources);

                sw.Stop();

                Trace.TraceInformation("[{0}]: Emitted {1} in {2}ms", GetType().Name, buildContext.AssemblyName, sw.ElapsedMilliseconds);

                if (!result.Success)
                {
                    diagnostics.AddRange(result.Diagnostics);
                    return false;
                }

                if (compilationContext.Diagnostics.Any(d => d.IsWarningAsError ||
                                                       d.Severity == DiagnosticSeverity.Error))
                {
                    return false;
                }

                // Ensure there's an output directory
                Directory.CreateDirectory(buildContext.OutputPath);

                assemblyStream.Position = 0;
                pdbStream.Position = 0;

                using (var pdbFileStream = File.Create(pdbPath))
                using (var assemblyFileStream = File.Create(assemblyPath))
                {
                    assemblyStream.CopyTo(assemblyFileStream);
                    pdbStream.CopyTo(pdbFileStream);
                }

                return true;
            }
        }
    }
}
