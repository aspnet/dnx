// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;
using NuGet;
using NuGet.ContentModel;

namespace Microsoft.Dnx.Tooling.Utils
{
    internal static class LockFileUtils
    {
        private static readonly Func<string, object> PlaceholderFileParser =
            s => string.Equals(s, "_._", StringComparison.Ordinal) ? s : null;

        public static LockFilePackageLibrary CreateLockFilePackageLibrary(LockFilePackageLibrary previousLibrary, IPackagePathResolver resolver, IPackage package, string correctedPackageName = null)
        {
            var lockFileLib = new LockFilePackageLibrary();

            // package.Id is read from nuspec and it might be in wrong casing.
            // correctedPackageName should be the package name used by dependency graph and
            // it has the correct casing that runtime needs during dependency resolution.
            lockFileLib.Name = correctedPackageName ?? package.Id;
            lockFileLib.Version = package.Version;
            lockFileLib.Sha512 = File.ReadAllText(resolver.GetHashPath(package.Id, package.Version));

            // If the shas are equal then do nothing
            if (previousLibrary?.Sha512 == lockFileLib.Sha512)
            {
                lockFileLib.Files = previousLibrary.Files;
                lockFileLib.IsServiceable = previousLibrary.IsServiceable;
            }
            else
            {
                lockFileLib.Files = package.GetFiles().Select(p => p.Path).ToList();
                var installPath = resolver.GetInstallPath(package.Id, package.Version);
                foreach (var filePath in lockFileLib.Files)
                {
                    if (!string.Equals(Path.GetExtension(filePath), ".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var assemblyPath = Path.Combine(installPath, filePath);
                    try
                    {
                        if (IsAssemblyServiceable(assemblyPath))
                        {
                            lockFileLib.IsServiceable = true;
                            break;
                        }
                    }
                    catch
                    {
                        // Just move on to the next file
                    }
                }
            }

            return lockFileLib;
        }

        public static LockFileProjectLibrary CreateLockFileProjectLibrary(LockFileProjectLibrary previousLibrary,
                                                                          Runtime.Project project,
                                                                          Runtime.Project library)
        {
            var result = new LockFileProjectLibrary()
            {
                Name = library.Name,
                Version = library.Version
            };

            if (previousLibrary?.Name == library.Name && previousLibrary?.Version == library.Version)
            {
                result.Path = previousLibrary.Path;
            }
            else
            {
                result.Path = PathUtility.GetRelativePath(project.ProjectFilePath, library.ProjectFilePath, '/');
            }

            return result;
        }

        public static LockFileTargetLibrary CreateLockFileTargetLibrary(LockFileProjectLibrary library,
                                                                        Runtime.Project projectInfo,
                                                                        RestoreContext context)
        {
            var lockFileLib = new LockFileTargetLibrary
            {
                Name = library.Name,
                Version = library.Version,
                Type = "project"
            };

            var targetFrameworkInfo = projectInfo.GetTargetFramework(context.FrameworkName);
            var dependencies = projectInfo.Dependencies.Concat(targetFrameworkInfo.Dependencies);

            foreach (var dependency in dependencies)
            {
                if (dependency.LibraryRange.IsGacOrFrameworkReference)
                {
                    lockFileLib.FrameworkAssemblies.Add(
                        LibraryRange.GetAssemblyName(dependency.LibraryRange.Name));
                }
                else
                {
                    lockFileLib.Dependencies.Add(new PackageDependency(
                        dependency.LibraryRange.Name, 
                        dependency.LibraryRange.VersionRange));
                }
            }

            return lockFileLib;
        }

        public static LockFileTargetLibrary CreateLockFileTargetLibrary(LockFilePackageLibrary library,
                                                                        IPackage package,
                                                                        RestoreContext context,
                                                                        string correctedPackageName)
        {
            var lockFileLib = new LockFileTargetLibrary { Type = "package" };

            var framework = context.FrameworkName;
            var runtimeIdentifier = context.RuntimeName;

            // package.Id is read from nuspec and it might be in wrong casing.
            // correctedPackageName should be the package name used by dependency graph and
            // it has the correct casing that runtime needs during dependency resolution.
            lockFileLib.Name = correctedPackageName ?? package.Id;
            lockFileLib.Version = package.Version;

            var files = library.Files.Select(p => p.Replace(Path.DirectorySeparatorChar, '/'));
            var contentItems = new ContentItemCollection();
            contentItems.Load(files);

            IEnumerable<PackageDependencySet> dependencySet;
            if (VersionUtility.GetNearest(framework, package.DependencySets, out dependencySet))
            {
                var set = dependencySet.FirstOrDefault()?.Dependencies?.ToList();

                if (set != null)
                {
                    lockFileLib.Dependencies = set;
                }
            }

            // TODO: Remove this when we do #596
            // ASP.NET Core and .NET Core 5.0 don't have framework reference assemblies
            if (!VersionUtility.IsPackageBased(framework))
            {
                IEnumerable<FrameworkAssemblyReference> frameworkAssemblies;
                if (VersionUtility.GetNearest(framework, package.FrameworkAssemblies, out frameworkAssemblies))
                {
                    AddFrameworkReferences(lockFileLib, framework, frameworkAssemblies);
                }

                // Add framework assemblies with empty supported frameworks
                AddFrameworkReferences(lockFileLib, framework, package.FrameworkAssemblies.Where(f => !f.SupportedFrameworks.Any()));
            }

            var patterns = PatternDefinitions.DotNetPatterns;

            var criteriaBuilderWithTfm = new SelectionCriteriaBuilder(patterns.Properties.Definitions);
            var criteriaBuilderWithoutTfm = new SelectionCriteriaBuilder(patterns.Properties.Definitions);

            if (context.RuntimeSpecs != null)
            {
                foreach (var runtimeSpec in context.RuntimeSpecs)
                {
                    criteriaBuilderWithTfm = criteriaBuilderWithTfm
                        .Add["tfm", framework]["rid", runtimeSpec.Name];

                    criteriaBuilderWithoutTfm = criteriaBuilderWithoutTfm
                        .Add["rid", runtimeSpec.Name];
                }
            }

            criteriaBuilderWithTfm = criteriaBuilderWithTfm
                .Add["tfm", framework];

            var criteria = criteriaBuilderWithTfm.Criteria;

            var compileGroup = contentItems.FindBestItemGroup(criteria, patterns.CompileTimeAssemblies, patterns.ManagedAssemblies);
            if (compileGroup != null)
            {
                lockFileLib.CompileTimeAssemblies = compileGroup.Items.Select(t => (LockFileItem)t.Path).ToList();
            }

            var runtimeGroup = contentItems.FindBestItemGroup(criteria, patterns.ManagedAssemblies);
            if (runtimeGroup != null)
            {
                lockFileLib.RuntimeAssemblies = runtimeGroup.Items.Select(p => (LockFileItem)p.Path).ToList();
            }

            var resourceGroup = contentItems.FindBestItemGroup(criteria, patterns.ResourceAssemblies);
            if (resourceGroup != null)
            {
                lockFileLib.ResourceAssemblies = resourceGroup.Items.Select(ToResourceLockFileItem).ToList();
            }

            var nativeGroup = contentItems.FindBestItemGroup(criteriaBuilderWithoutTfm.Criteria, patterns.NativeLibraries);
            if (nativeGroup != null)
            {
                lockFileLib.NativeLibraries = nativeGroup.Items.Select(p => (LockFileItem)p.Path).ToList();
            }

            // COMPAT: Support lib/contract so older packages can be consumed
            string contractPath = "lib/contract/" + package.Id + ".dll";
            var hasContract = files.Any(path => path == contractPath);
            var hasLib = lockFileLib.RuntimeAssemblies.Any();

            if (hasContract && hasLib && !VersionUtility.IsDesktop(framework))
            {
                lockFileLib.CompileTimeAssemblies.Clear();
                lockFileLib.CompileTimeAssemblies.Add(contractPath);
            }

            // See if there's a list of specific references defined for this target framework
            IEnumerable<PackageReferenceSet> referenceSets;
            if (VersionUtility.GetNearest(framework, package.PackageAssemblyReferences, out referenceSets))
            {
                // Get the first compatible reference set
                var referenceSet = referenceSets.FirstOrDefault();

                if (referenceSet != null)
                {
                    // Remove all compile-time assemblies of which names do not appear in the References list
                    lockFileLib.CompileTimeAssemblies.RemoveAll(path => path.Path.StartsWith("lib/") && !referenceSet.References.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase));
                }
            }

            return lockFileLib;
        }

        private static LockFileItem ToResourceLockFileItem(ContentItem item)
        {
            return new LockFileItem
            {
                Path = item.Path,
                Properties =
                {
                    { "locale", item.Properties["locale"].ToString()}
                }
            };
        }

        private static void AddFrameworkReferences(LockFileTargetLibrary lockFileLib, FrameworkName framework, IEnumerable<FrameworkAssemblyReference> frameworkAssemblies)
        {
            foreach (var assemblyReference in frameworkAssemblies)
            {
                if (!assemblyReference.SupportedFrameworks.Any() &&
                    !VersionUtility.IsDesktop(framework))
                {
                    // REVIEW: This isn't 100% correct since none *can* mean 
                    // any in theory, but in practice it means .NET full reference assembly
                    // If there's no supported target frameworks and we're not targeting
                    // the desktop framework then skip it.

                    // To do this properly we'll need all reference assemblies supported
                    // by each supported target framework which isn't always available.
                    continue;
                }

                lockFileLib.FrameworkAssemblies.Add(assemblyReference.AssemblyName);
            }
        }

        private static List<string> GetPackageAssemblies(IPackage package, FrameworkName targetFramework)
        {
            var results = new List<string>();

            IEnumerable<IPackageAssemblyReference> compatibleReferences;
            if (VersionUtility.GetNearest(targetFramework, package.AssemblyReferences, out compatibleReferences))
            {
                // Get the list of references for this target framework
                var references = compatibleReferences.ToList();

                // See if there's a list of specific references defined for this target framework
                IEnumerable<PackageReferenceSet> referenceSets;
                if (VersionUtility.GetNearest(targetFramework, package.PackageAssemblyReferences, out referenceSets))
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

        internal static bool IsAssemblyServiceable(string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
            {
                return false;
            }

            using (var stream = File.OpenRead(assemblyPath))
            using (var peReader = new PEReader(stream))
            {
                if (!peReader.HasMetadata)
                {
                    return false;
                }

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

        public class PropertyDefinitions
        {
            public PropertyDefinitions()
            {
                Definitions = new Dictionary<string, ContentPropertyDefinition>
                {
                    { "language", _language },
                    { "tfm", _targetFramework },
                    { "rid", _rid },
                    { "assembly", _assembly },
                    { "dynamicLibrary", _dynamicLibrary },
                    { "resources", _resources },
                    { "locale", _locale },
                    { "any", _any },
                };
            }

            public IDictionary<string, ContentPropertyDefinition> Definitions { get; }

            ContentPropertyDefinition _language = new ContentPropertyDefinition
            {
                Table =
                {
                    { "cs", "CSharp" },
                    { "vb", "Visual Basic" },
                    { "fs", "FSharp" },
                }
            };

            ContentPropertyDefinition _targetFramework = new ContentPropertyDefinition
            {
                Table =
                {
                    { "any", new FrameworkName(VersionUtility.NetPlatformFrameworkIdentifier, new Version(5, 0)) }
                },
                Parser = TargetFrameworkName_Parser,
                OnIsCriteriaSatisfied = TargetFrameworkName_IsCriteriaSatisfied,
                OnCompare = TargetFrameworkName_NearestCompareTest
            };

            ContentPropertyDefinition _rid = new ContentPropertyDefinition
            {
                Parser = name => name
            };

            ContentPropertyDefinition _assembly = new ContentPropertyDefinition
            {
                Parser = PlaceholderFileParser,
                FileExtensions = { ".dll", ".exe", ".winmd" }
            };

            ContentPropertyDefinition _dynamicLibrary = new ContentPropertyDefinition
            {
                Parser = PlaceholderFileParser,
                FileExtensions = { ".dll", ".dylib", ".so" }
            };

            ContentPropertyDefinition _resources = new ContentPropertyDefinition
            {
                FileExtensions = { ".resources.dll" }
            };

            ContentPropertyDefinition _locale = new ContentPropertyDefinition
            {
                Parser = Locale_Parser,
            };

            ContentPropertyDefinition _any = new ContentPropertyDefinition
            {
                Parser = name => name
            };


            internal static object Locale_Parser(string name)
            {
                if (name.Length == 2)
                {
                    return name;
                }
                else if (name.Length >= 4 && name[2] == '-')
                {
                    return name;
                }

                return null;
            }

            internal static object TargetFrameworkName_Parser(string name)
            {
                var result = VersionUtility.ParseFrameworkName(name);

                if (result != VersionUtility.UnsupportedFrameworkName)
                {
                    return result;
                }

                return new FrameworkName(name, new Version(0, 0));
            }

            internal static bool TargetFrameworkName_IsCriteriaSatisfied(object criteria, object available)
            {
                var criteriaFrameworkName = criteria as FrameworkName;
                var availableFrameworkName = available as FrameworkName;

                if (criteriaFrameworkName != null && availableFrameworkName != null)
                {
                    return VersionUtility.IsCompatible(criteriaFrameworkName, availableFrameworkName);
                }

                return false;
            }

            private static int TargetFrameworkName_NearestCompareTest(object projectFramework, object criteria, object available)
            {
                var projectFrameworkName = projectFramework as FrameworkName;
                var criteriaFrameworkName = criteria as FrameworkName;
                var availableFrameworkName = available as FrameworkName;

                if (criteriaFrameworkName != null
                    && availableFrameworkName != null
                    && projectFrameworkName != null)
                {
                    var frameworks = new[] { criteriaFrameworkName, availableFrameworkName };

                    // Find the nearest compatible framework to the project framework.
                    var nearest = VersionUtility.GetNearest(projectFrameworkName, frameworks);

                    if (nearest == null)
                    {
                        return -1;
                    }

                    if (criteriaFrameworkName.Equals(nearest))
                    {
                        return -1;
                    }

                    if (availableFrameworkName.Equals(nearest))
                    {
                        return 1;
                    }
                }

                return 0;
            }

            private class GetNearestHelper : IFrameworkTargetable
            {
                public FrameworkName Framework { get; }

                public IEnumerable<FrameworkName> SupportedFrameworks
                {
                    get
                    {
                        yield return Framework;
                    }
                }

                public GetNearestHelper(FrameworkName framework) { Framework = framework; }


            }
        }

        public class PatternDefinitions
        {
            public static readonly PatternDefinitions DotNetPatterns = new PatternDefinitions();

            public PropertyDefinitions Properties { get; }

            public ContentPatternDefinition CompileTimeAssemblies { get; }
            public ContentPatternDefinition ManagedAssemblies { get; }
            public ContentPatternDefinition ResourceAssemblies { get; }
            public ContentPatternDefinition NativeLibraries { get; }

            public PatternDefinitions()
            {
                Properties = new PropertyDefinitions();

                ManagedAssemblies = new ContentPatternDefinition
                {
                    GroupPatterns =
                    {
                        "runtimes/{rid}/lib/{tfm}/{any?}",
                        "lib/{tfm}/{any?}"
                    },
                    PathPatterns =
                    {
                        "runtimes/{rid}/lib/{tfm}/{assembly}",
                        "lib/{tfm}/{assembly}"
                    },
                    PropertyDefinitions = Properties.Definitions
                };

                ManagedAssemblies.GroupPatterns.Add(new PatternDefinition
                {
                    Pattern = "lib/{assembly?}",
                    Defaults = new Dictionary<string, object>
                    {
                        {  "tfm", VersionUtility.ParseFrameworkName("net") }
                    }
                });

                ManagedAssemblies.PathPatterns.Add(new PatternDefinition
                {
                    Pattern = "lib/{assembly}",
                    Defaults = new Dictionary<string, object>
                    {
                        {  "tfm", VersionUtility.ParseFrameworkName("net") }
                    }
                });

                CompileTimeAssemblies = new ContentPatternDefinition
                {
                    GroupPatterns =
                    {
                        "ref/{tfm}/{any?}",
                    },
                    PathPatterns =
                    {
                        "ref/{tfm}/{assembly}",
                    },
                    PropertyDefinitions = Properties.Definitions,
                };

                ResourceAssemblies = new ContentPatternDefinition
                {
                    GroupPatterns =
                    {
                        "runtimes/{rid}/lib/{tfm}/{locale?}/{any?}",
                        "lib/{tfm}/{locale?}/{any?}"
                    },
                    PathPatterns =
                    {
                        "runtimes/{rid}/lib/{tfm}/{locale}/{resources}",
                        "lib/{tfm}/{locale}/{resources}"
                    },
                    PropertyDefinitions = Properties.Definitions
                };

                ResourceAssemblies.GroupPatterns.Add(new PatternDefinition
                {
                    Pattern = "lib/{locale}/{resources?}",
                    Defaults = new Dictionary<string, object>
                    {
                        {  "tfm", VersionUtility.ParseFrameworkName("net") }
                    }
                });

                ResourceAssemblies.PathPatterns.Add(new PatternDefinition
                {
                    Pattern = "lib/{locale}/{resources}",
                    Defaults = new Dictionary<string, object>
                    {
                        {  "tfm", VersionUtility.ParseFrameworkName("net") }
                    }
                });

                NativeLibraries = new ContentPatternDefinition
                {
                    GroupPatterns =
                    {
                        "runtimes/{rid}/native/{any?}",
                        "native/{any?}",
                    },
                    PathPatterns =
                    {
                        "runtimes/{rid}/native/{any}",
                        "native/{any}",
                    },
                    PropertyDefinitions = Properties.Definitions,
                };
            }
        }

        private class SelectionCriteriaBuilder
        {
            private IDictionary<string, ContentPropertyDefinition> propertyDefinitions;

            public SelectionCriteriaBuilder(IDictionary<string, ContentPropertyDefinition> propertyDefinitions)
            {
                this.propertyDefinitions = propertyDefinitions;
            }

            public virtual SelectionCriteria Criteria { get; } = new SelectionCriteria();

            internal virtual SelectionCriteriaEntryBuilder Add
            {
                get
                {
                    var entry = new SelectionCriteriaEntry();
                    Criteria.Entries.Add(entry);
                    return new SelectionCriteriaEntryBuilder(this, entry);
                }
            }

            internal class SelectionCriteriaEntryBuilder : SelectionCriteriaBuilder
            {
                public SelectionCriteriaEntry Entry { get; }
                public SelectionCriteriaBuilder Builder { get; }

                public SelectionCriteriaEntryBuilder(SelectionCriteriaBuilder builder, SelectionCriteriaEntry entry) : base(builder.propertyDefinitions)
                {
                    Builder = builder;
                    Entry = entry;
                }
                public SelectionCriteriaEntryBuilder this[string key, string value]
                {
                    get
                    {
                        ContentPropertyDefinition propertyDefinition;
                        if (!propertyDefinitions.TryGetValue(key, out propertyDefinition))
                        {
                            throw new Exception("Undefined property used for criteria");
                        }
                        if (value == null)
                        {
                            Entry.Properties[key] = null;
                        }
                        else
                        {
                            object valueLookup;
                            if (propertyDefinition.TryLookup(value, out valueLookup))
                            {
                                Entry.Properties[key] = valueLookup;
                            }
                            else
                            {
                                throw new Exception("Undefined value used for criteria");
                            }
                        }
                        return this;
                    }
                }
                public SelectionCriteriaEntryBuilder this[string key, object value]
                {
                    get
                    {
                        ContentPropertyDefinition propertyDefinition;
                        if (!propertyDefinitions.TryGetValue(key, out propertyDefinition))
                        {
                            throw new Exception("Undefined property used for criteria");
                        }
                        Entry.Properties[key] = value;
                        return this;
                    }
                }
                internal override SelectionCriteriaEntryBuilder Add
                {
                    get
                    {
                        return Builder.Add;
                    }
                }
                public override SelectionCriteria Criteria
                {
                    get
                    {
                        return Builder.Criteria;
                    }
                }
            }
        }
    }
}
