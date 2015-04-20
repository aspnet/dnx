using System;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Repositories;
using NuGet.ProjectModel;

namespace Microsoft.Framework.PackageManager.Utils
{
    internal static class LockFileUtils
    {
        // DNU REFACTORING TODO: leave this for incremental refactoring, remove after refactoring "dnu publish"
        public static Microsoft.Framework.Runtime.DependencyManagement.LockFileLibrary CreateLockFileLibraryForProject(
            Runtime.Project project,
            NuGet.IPackage package,
            SHA512 sha512,
            IEnumerable<System.Runtime.Versioning.FrameworkName> frameworks,
            NuGet.IPackagePathResolver resolver,
            string correctedPackageName = null)
        {
            var lockFileLib = new Microsoft.Framework.Runtime.DependencyManagement.LockFileLibrary();

            // package.Id is read from nuspec and it might be in wrong casing.
            // correctedPackageName should be the package name used by dependency graph and
            // it has the correct casing that runtime needs during dependency resolution.
            lockFileLib.Name = correctedPackageName ?? package.Id;
            lockFileLib.Version = package.Version;

            using (var nupkgStream = package.GetStream())
            {
                lockFileLib.Sha = Convert.ToBase64String(sha512.ComputeHash(nupkgStream));
            }
            lockFileLib.Files = package.GetFiles().Select(p => p.Path).ToList();

            foreach (var framework in frameworks)
            {
                var group = new Microsoft.Framework.Runtime.DependencyManagement.LockFileFrameworkGroup();
                group.TargetFramework = framework;

                IEnumerable<NuGet.PackageDependencySet> dependencySet;
                if (NuGet.VersionUtility.TryGetCompatibleItems(framework, package.DependencySets, out dependencySet))
                {
                    var set = dependencySet.FirstOrDefault()?.Dependencies?.ToList();

                    if (set != null)
                    {
                        group.Dependencies = set;
                    }
                }

                // TODO: Remove this when we do #596
                // ASP.NET Core isn't compatible with generic PCL profiles
                if (!string.Equals(framework.Identifier, NuGet.VersionUtility.AspNetCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(framework.Identifier, NuGet.VersionUtility.DnxCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
                {
                    IEnumerable<NuGet.FrameworkAssemblyReference> frameworkAssemblies;
                    if (NuGet.VersionUtility.TryGetCompatibleItems(framework, package.FrameworkAssemblies, out frameworkAssemblies))
                    {
                        foreach (var assemblyReference in frameworkAssemblies)
                        {
                            if (!assemblyReference.SupportedFrameworks.Any() &&
                                !NuGet.VersionUtility.IsDesktop(framework))
                            {
                                // REVIEW: This isn't 100% correct since none *can* mean 
                                // any in theory, but in practice it means .NET full reference assembly
                                // If there's no supported target frameworks and we're not targeting
                                // the desktop framework then skip it.

                                // To do this properly we'll need all reference assemblies supported
                                // by each supported target framework which isn't always available.
                                continue;
                            }

                            group.FrameworkAssemblies.Add(assemblyReference);
                        }
                    }
                }

                group.RuntimeAssemblies = GetPackageAssemblies(package, framework);

                string contractPath = Path.Combine("lib", "contract", package.Id + ".dll");
                var hasContract = lockFileLib.Files.Any(path => path == contractPath);
                var hasLib = group.RuntimeAssemblies.Any();

                if (hasContract && hasLib && !NuGet.VersionUtility.IsDesktop(framework))
                {
                    group.CompileTimeAssemblies.Add(contractPath);
                }
                else if (hasLib)
                {
                    group.CompileTimeAssemblies.AddRange(group.RuntimeAssemblies);
                }

                lockFileLib.FrameworkGroups.Add(group);
            }

            var installPath = resolver.GetInstallPath(package.Id, package.Version);
            foreach (var assembly in lockFileLib.FrameworkGroups.SelectMany(f => f.RuntimeAssemblies))
            {
                var assemblyPath = Path.Combine(installPath, assembly);
                if (IsAssemblyServiceable(assemblyPath))
                {
                    lockFileLib.IsServiceable = true;
                    break;
                }
            }

            return lockFileLib;
        }

        private static List<string> GetPackageAssemblies(NuGet.IPackage package, System.Runtime.Versioning.FrameworkName targetFramework)
        {
            var results = new List<string>();

            IEnumerable<NuGet.IPackageAssemblyReference> compatibleReferences;
            if (NuGet.VersionUtility.TryGetCompatibleItems(targetFramework, package.AssemblyReferences, out compatibleReferences))
            {
                // Get the list of references for this target framework
                var references = compatibleReferences.ToList();

                // See if there's a list of specific references defined for this target framework
                IEnumerable<NuGet.PackageReferenceSet> referenceSets;
                if (NuGet.VersionUtility.TryGetCompatibleItems(targetFramework, package.PackageAssemblyReferences, out referenceSets))
                {
                    // Get the first compatible reference set
                    var referenceSet = referenceSets.FirstOrDefault();

                    if (referenceSet != null)
                    {
                        // Remove all assemblies of which names do not appear in the References list
                        references.RemoveAll(r => !referenceSet.References.Contains(r.Name, StringComparer.OrdinalIgnoreCase));
                    }
                }

                foreach (var reference in references)
                {
                    // Skip anything that isn't a dll. Unfortunately some packages put random stuff
                    // in the lib folder and they surface as assembly references
                    if (!Path.GetExtension(reference.Path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    results.Add(reference.Path);
                }
            }

            return results;
        }

        public static LockFileLibrary CreateLockFileLibrary(
            LocalPackageInfo packageInfo,
            SHA512 sha512,
            IEnumerable<NuGetFramework> frameworks,
            string correctedPackageName = null)
        {
            var lockFileLib = new LockFileLibrary();

            // package.Id is read from nuspec and it might be in wrong casing.
            // correctedPackageName should be the package name used by dependency graph and
            // it has the correct casing that runtime needs during dependency resolution.
            lockFileLib.Name = correctedPackageName ?? packageInfo.Id;
            lockFileLib.Version = packageInfo.Version;

            using (var nupkgStream = File.OpenRead(packageInfo.ZipPath))
            {
                lockFileLib.Sha = Convert.ToBase64String(sha512.ComputeHash(nupkgStream));
                nupkgStream.Seek(0, SeekOrigin.Begin);

                var packageReader = new PackageReader(nupkgStream);
                lockFileLib.Files = packageReader.GetFiles().ToList();

                foreach (var framework in frameworks)
                {
                    var dependencies = NuGetFrameworkUtility.GetNearest(packageReader.GetPackageDependencies(),
                        framework, item => item.TargetFramework)?.Packages.ToList();
                    var runtimeAssemblies = NuGetFrameworkUtility.GetNearest(packageReader.GetReferenceItems(),
                        framework, item => item.TargetFramework)?.Items.ToList();

                    // The compatibility mapping "dnxcore50 -> portable-net45+win8" is missing in NuGet core libs
                    // We need to do that check here
                    if (runtimeAssemblies == null &&
                        framework == FrameworkConstants.CommonFrameworks.DnxCore50)
                    {
                        runtimeAssemblies = packageReader.GetReferenceItems()
                            .FirstOrDefault(x => string.Equals(x.TargetFramework.Profile, "Profile7"))?.Items.ToList();
                    }

                    var frameworkAssembliesGroup = NuGetFrameworkUtility.GetNearest(packageReader.GetFrameworkItems(),
                        framework, item => item.TargetFramework);

                    var group = new LockFileFrameworkGroup();
                    group.TargetFramework = framework;
                    group.Dependencies = dependencies ?? new List<PackageDependency>();
                    group.RuntimeAssemblies = runtimeAssemblies ?? new List<string>();

                    if (frameworkAssembliesGroup != null)
                    {
                        // "targetFramework" is empty string for those framework assemblies
                        if (frameworkAssembliesGroup.TargetFramework == NuGetFramework.AnyFramework)
                        {
                            // REVIEW: This isn't 100% correct since none *can* mean 
                            // any in theory, but in practice it means .NET full reference assembly
                            // If there's no supported target frameworks and we're not targeting
                            // the desktop framework then skip it.

                            // To do this properly we'll need all reference assemblies supported
                            // by each supported target framework which isn't always available.
                            if (framework.IsDesktop())
                            {
                                group.FrameworkAssemblies = frameworkAssembliesGroup.Items.ToList();
                            }
                        }
                        else
                        {
                            group.FrameworkAssemblies = frameworkAssembliesGroup.Items.ToList();
                        }
                    }

                    string contractPath = Path.Combine("lib", "contract", packageInfo.Id + ".dll");
                    var hasContract = lockFileLib.Files.Any(path => path == contractPath);
                    var hasLib = group.RuntimeAssemblies.Any();

                    if (hasContract && hasLib && !framework.IsDesktop())
                    {
                        group.CompileTimeAssemblies.Add(contractPath);
                    }
                    else if (hasLib)
                    {
                        group.CompileTimeAssemblies.AddRange(group.RuntimeAssemblies);
                    }

                    lockFileLib.FrameworkGroups.Add(group);
                }
            }

            foreach (var assembly in lockFileLib.FrameworkGroups.SelectMany(f => f.RuntimeAssemblies))
            {
                var assemblyPath = Path.Combine(packageInfo.ExpandedPath, assembly);
                if (IsAssemblyServiceable(assemblyPath))
                {
                    lockFileLib.IsServiceable = true;
                    break;
                }
            }

            return lockFileLib;
        }

        internal static bool IsAssemblyServiceable(string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
            {
                return false;
            }

            using (var stream = File.OpenRead(assemblyPath))
            using (var peReader = new PEReader(stream))
            {
                var mdReader = peReader.GetMetadataReader();
                var attrs = mdReader.GetAssemblyDefinition().GetCustomAttributes()
                    .Select(ah => mdReader.GetCustomAttribute(ah));

                foreach (var attr in attrs)
                {
                    var ctorHandle = attr.Constructor;
                    if (ctorHandle.Kind != HandleKind.MemberReference)
                    {
                        continue;
                    }

                    var container = mdReader.GetMemberReference((MemberReferenceHandle)ctorHandle).Parent;
                    var name = mdReader.GetTypeReference((TypeReferenceHandle)container).Name;
                    if (!string.Equals(mdReader.GetString(name), "AssemblyMetadataAttribute"))
                    {
                        continue;
                    }

                    var arguments = GetFixedStringArguments(mdReader, attr);
                    if (arguments.Count == 2 &&
                        string.Equals(arguments[0], "Serviceable", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(arguments[1], "True", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the fixed (required) string arguments of a custom attribute.
        /// Only attributes that have only fixed string arguments.
        /// </summary>
        private static List<string> GetFixedStringArguments(MetadataReader reader, CustomAttribute attribute)
        {
            // TODO: Nick Guerrera (Nick.Guerrera@microsoft.com) hacked this method for temporary use.
            // There is a blob decoder feature in progress but it won't ship in time for our milestone.
            // Replace this method with the blob decoder feature when later it is availale.

            var signature = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Signature;
            var signatureReader = reader.GetBlobReader(signature);
            var valueReader = reader.GetBlobReader(attribute.Value);
            var arguments = new List<string>();

            var prolog = valueReader.ReadUInt16();
            if (prolog != 1)
            {
                // Invalid custom attribute prolog
                return arguments;
            }

            var header = signatureReader.ReadSignatureHeader();
            if (header.Kind != SignatureKind.Method || header.IsGeneric)
            {
                // Invalid custom attribute constructor signature
                return arguments;
            }

            int parameterCount;
            if (!signatureReader.TryReadCompressedInteger(out parameterCount))
            {
                // Invalid custom attribute constructor signature
                return arguments;
            }

            var returnType = signatureReader.ReadSignatureTypeCode();
            if (returnType != SignatureTypeCode.Void)
            {
                // Invalid custom attribute constructor signature
                return arguments;
            }

            for (int i = 0; i < parameterCount; i++)
            {
                var signatureTypeCode = signatureReader.ReadSignatureTypeCode();
                if (signatureTypeCode == SignatureTypeCode.String)
                {
                    // Custom attribute constructor must take only strings
                    arguments.Add(valueReader.ReadSerializedString());
                }
            }

            return arguments;
        }
    }
}