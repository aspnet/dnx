using System;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Framework.PackageManager.NuGetUtils;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Repositories;
using NuGet.ProjectModel;

namespace Microsoft.Framework.PackageManager.Utils
{
    internal static class LockFileUtils
    {
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
                    var group = new LockFileFrameworkGroup();
                    group.TargetFramework = framework;
                    group.Dependencies = packageReader.GetPackageDependencies()
                        .FirstOrDefault(x => x.TargetFramework == framework)?.Packages.ToList();
                    group.FrameworkAssemblies = packageReader.GetFrameworkItems()
                        .FirstOrDefault(x => x.TargetFramework == framework)?.Items.ToList();
                    group.RuntimeAssemblies = packageReader.GetReferenceItems()
                        .FirstOrDefault(x => x.TargetFramework == framework)?.Items.ToList();

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