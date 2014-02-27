using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using NuGet;
using Microsoft.Net.Runtime.Loader;
using System.Runtime.Versioning;

#if NET45 // TODO: Temporary due to CoreCLR and Desktop Roslyn being out of sync
using EmitResult = Microsoft.CodeAnalysis.Emit.CommonEmitResult;
#endif

namespace Microsoft.Net.Runtime.Roslyn
{
    public class RoslynArtifactsProducer
    {
        private readonly IRoslynCompiler _compiler;
        private readonly IResourceProvider _resourceProvider;
        private readonly IFrameworkReferenceResolver _frameworkReferenceResolver;
        private IEnumerable<DependencyDescription> _resolvedDependencies;

        public RoslynArtifactsProducer(IRoslynCompiler compiler,
                                       IResourceProvider resourceProvider,
                                       IFrameworkReferenceResolver frameworkReferenceResolver,
                                       IEnumerable<DependencyDescription> resolvedDependencies)
        {
            _compiler = compiler;
            _resourceProvider = resourceProvider;
            _frameworkReferenceResolver = frameworkReferenceResolver;
            _resolvedDependencies = resolvedDependencies;
        }

        public bool Build(BuildContext buildContext, List<Diagnostic> diagnostics)
        {
            var compilationContext = _compiler.CompileProject(buildContext.AssemblyName, buildContext.TargetFramework);

            if (compilationContext == null)
            {
                return false;
            }

            var project = compilationContext.Project;
            var name = project.Name;

            var resources = _resourceProvider.GetResources(project);

            foreach (var reference in compilationContext.AssemblyNeutralReferences)
            {
                resources.Add(new ResourceDescription(reference.Name + ".dll",
                                                      () => reference.OutputStream,
                                                      isPublic: true));
            }

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
            // TODO: Build symbols packages
        }

        private void BuildPackage(BuildContext buildContext, CompilationContext compilationContext, string assemblyPath)
        {
            var framework = buildContext.TargetFramework;
            var dependencies = new List<PackageDependency>();
            var project = compilationContext.Project;
            var projectReferenceByName = compilationContext.ProjectReferences.ToDictionary(p => p.Project.Name, p => p.Project);

            var frameworkReferences = new HashSet<string>(_frameworkReferenceResolver.GetFrameworkReferences(framework), StringComparer.OrdinalIgnoreCase);
            var frameworkAssemblies = new List<string>();

            if (project.Dependencies.Count > 0)
            {
                foreach (var dependency in project.Dependencies)
                {
                    Project dependencyProject;
                    if (projectReferenceByName.TryGetValue(dependency.Name, out dependencyProject) && dependencyProject.EmbedInteropTypes)
                    {
                        continue;
                    }

                    if (frameworkReferences.Contains(dependency.Name))
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
                    buildContext.PackageBuilder.DependencySets.Add(new PackageDependencySet(framework, dependencies));
                }
            }

            // Only do this on full desktop
            if (buildContext.TargetFramework.Identifier == VersionUtility.DefaultTargetFramework.Identifier)
            {
                foreach (var a in frameworkAssemblies)
                {
                    buildContext.PackageBuilder.FrameworkReferences.Add(new FrameworkAssemblyReference(a));
                }
            }

            var file = new PhysicalPackageFile();
            file.SourcePath = assemblyPath;
            var folder = VersionUtility.GetShortFrameworkName(framework);
            file.TargetPath = String.Format(@"lib\{0}\{1}.dll", folder, project.Name);
            buildContext.PackageBuilder.Files.Add(file);
        }

        private bool CompileToDisk(BuildContext buildContext, string assemblyPath, string pdbPath, CompilationContext compilationContext, IList<ResourceDescription> resources, List<Diagnostic> diagnostics)
        {
            // REVIEW: Memory bloat?
            using (var pdbStream = new MemoryStream())
            using (var assemblyStream = new MemoryStream())
            {
#if NET45
                EmitResult result = compilationContext.Compilation.Emit(assemblyStream, outputName: Path.GetFileName(assemblyPath), pdbFileName: pdbPath, pdbStream: pdbStream, manifestResources: resources);
#else
                EmitResult result = compilationContext.Compilation.Emit(assemblyStream);
#endif

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
