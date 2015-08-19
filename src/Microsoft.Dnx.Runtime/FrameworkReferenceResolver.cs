// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Xml.Linq;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class FrameworkReferenceResolver : IFrameworkReferenceResolver
    {
        private readonly IDictionary<FrameworkName, FrameworkInformation> _cache = new Dictionary<FrameworkName, FrameworkInformation>();

        private static readonly IDictionary<FrameworkName, List<FrameworkName>> _aliases = new Dictionary<FrameworkName, List<FrameworkName>>
        {
            { new FrameworkName(VersionUtility.AspNetFrameworkIdentifier, new Version(5, 0)), new List<FrameworkName> {
                    new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 5, 1))
                }
            },
            { new FrameworkName(VersionUtility.DnxFrameworkIdentifier, new Version(4, 5, 1)), new List<FrameworkName> {
                    new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 5, 1))
                }
            },
            { new FrameworkName(VersionUtility.DnxFrameworkIdentifier, new Version(4, 6)), new List<FrameworkName> {
                    new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 6))
                }
            }
        };

        private static readonly IDictionary<FrameworkName, List<FrameworkName>> _monoAliases = new Dictionary<FrameworkName, List<FrameworkName>>
        {
            { new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 5, 1)), new List<FrameworkName> {
                    new FrameworkName(VersionUtility.DnxFrameworkIdentifier, new Version(4, 5, 1)),
                    new FrameworkName(VersionUtility.AspNetFrameworkIdentifier, new Version(5, 0))
                }
            }
        };

        public bool TryGetAssembly(string name, FrameworkName targetFramework, out string path, out Version version)
        {
            path = null;
            version = null;

            var information = _cache.GetOrAdd(targetFramework, GetFrameworkInformation);

            if (information == null || !information.Exists)
            {
                return false;
            }

            lock (information.Assemblies)
            {
                AssemblyEntry entry;
                if (information.Assemblies.TryGetValue(name, out entry))
                {
                    if (string.IsNullOrEmpty(entry.Path))
                    {
                        entry.Path = GetAssemblyPath(information.SearchPaths, name);
                    }

                    if (!string.IsNullOrEmpty(entry.Path) && entry.Version == null)
                    {
                        // This code path should only run on mono
                        entry.Version = VersionUtility.GetAssemblyVersion(entry.Path).Version;
                    }

                    path = entry.Path;
                    version = entry.Version;
                }
            }

            return !string.IsNullOrEmpty(path);
        }

        public string GetFriendlyFrameworkName(FrameworkName targetFramework)
        {
            // We don't have a friendly name for this anywhere on the machine so hard code it
            if (string.Equals(targetFramework.Identifier, "K", StringComparison.OrdinalIgnoreCase))
            {
                return ".NET Core Framework 4.5";
            }
            else if (string.Equals(targetFramework.Identifier, VersionUtility.AspNetCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                return "ASP.NET Core 5.0";
            }
            else if (string.Equals(targetFramework.Identifier, VersionUtility.AspNetFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                return "ASP.NET 5.0";
            }
            else if (string.Equals(targetFramework.Identifier, VersionUtility.DnxCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                return "DNX Core 5.0";
            }
            else if (string.Equals(targetFramework.Identifier, VersionUtility.DnxFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                return "DNX " + targetFramework.Version.ToString();
            }
            else if (string.Equals(targetFramework.Identifier, VersionUtility.NetPlatformFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                return ".NET Platform";
            }

            var information = _cache.GetOrAdd(targetFramework, GetFrameworkInformation);

            if (information == null) 
            {
                return SynthesizeFrameworkFriendlyName(targetFramework);
            }

            return information.Name;
        }

        public IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            var information = _cache.GetOrAdd(targetFramework, GetFrameworkInformation);

            if (information == null || !information.Exists)
            {
                return null;
            }
            return information.SearchPaths.Select(s => Path.Combine(s, "{name}.dll"));
        }

        public string GetFrameworkRedistListPath(FrameworkName targetFramework)
        {
            var information = _cache.GetOrAdd(targetFramework, GetFrameworkInformation);

            if (information == null || !information.Exists)
            {
                return null;
            }

            return information.RedistListPath;
        }

        public static string GetReferenceAssembliesPath()
        {
#if DNX451
            if (RuntimeEnvironmentHelper.IsMono)
            {
                var mscorlibLocationOnThisRunningMonoInstance = typeof(object).GetTypeInfo().Assembly.Location;

                var libPath = Path.GetDirectoryName(Path.GetDirectoryName(mscorlibLocationOnThisRunningMonoInstance));

                return Path.Combine(libPath, "xbuild-frameworks");
            }
#endif

            // References assemblies are in %ProgramFiles(x86)% on
            // 64 bit machines
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles(x86)");

            if (string.IsNullOrEmpty(programFiles))
            {
                // On 32 bit machines they are in %ProgramFiles%
                programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            }

            if (string.IsNullOrEmpty(programFiles))
            {
                // Reference assemblies aren't installed
                return null;
            }

            return Path.Combine(
                    programFiles,
                    "Reference Assemblies", "Microsoft", "Framework");
        }

        private static FrameworkInformation GetFrameworkInformation(FrameworkName targetFramework)
        {
            string referenceAssembliesPath = GetReferenceAssembliesPath();

            if (string.IsNullOrEmpty(referenceAssembliesPath))
            {
                return null;
            }

            FrameworkInformation frameworkInfo;

            // Skip this on mono since it has a slightly different set of reference assemblies at a different
            // location
            if (!RuntimeEnvironmentHelper.IsMono
                && FrameworkDefinitions.TryPopulateFrameworkFastPath(targetFramework.Identifier, targetFramework.Version, referenceAssembliesPath, out frameworkInfo))
            {
                return frameworkInfo;
            }

            List<FrameworkName> candidates;
            if (_aliases.TryGetValue(targetFramework, out candidates))
            {
                foreach (var framework in candidates)
                {
                    var information = GetFrameworkInformation(framework);

                    if (information != null)
                    {
                        return information;
                    }
                }

                return null;
            }
            else
            {
                return GetFrameworkInformation(targetFramework, referenceAssembliesPath);
            }
        }

        private static FrameworkInformation GetFrameworkInformation(FrameworkName targetFramework, string referenceAssembliesPath)
        {
            // Check for legacy frameworks
            if (targetFramework.Identifier.Equals(VersionUtility.NetFrameworkIdentifier, StringComparison.Ordinal) && targetFramework.Version <= Constants.Version35)
            {
                return GetLegacyFrameworkInformation(targetFramework, referenceAssembliesPath);
            }

            var basePath = Path.Combine(referenceAssembliesPath,
                                        targetFramework.Identifier,
                                        "v" + targetFramework.Version);

            if (!string.IsNullOrEmpty(targetFramework.Profile))
            {
                basePath = Path.Combine(basePath, "Profile", targetFramework.Profile);
            }

            var version = new DirectoryInfo(basePath);
            if (!version.Exists)
            {
                return null;
            }

            return GetFrameworkInformation(version, targetFramework);
        }

        private static FrameworkInformation GetLegacyFrameworkInformation(FrameworkName targetFramework, string referenceAssembliesPath)
        {
            var frameworkInfo = new FrameworkInformation();
            if (!RuntimeEnvironmentHelper.IsMono)
            {
                // Always grab .NET 2.0 data
                var searchPaths = new List<string>();
                var net20Dir = Path.Combine(Environment.GetEnvironmentVariable("WINDIR"), "Microsoft.NET", "Framework", "v2.0.50727");

                if (!Directory.Exists(net20Dir))
                {
                    return null;
                }

                // Grab reference assemblies first, if present for this framework
                if (targetFramework.Version.Major == 3)
                {
                    // Most specific first (i.e. 3.5)
                    if (targetFramework.Version.Minor == 5)
                    {
                        var refAsms35Dir = Path.Combine(referenceAssembliesPath, "v3.5");
                        if (Directory.Exists(refAsms35Dir))
                        {
                            searchPaths.Add(refAsms35Dir);
                        }
                    }

                    // Always search the 3.0 reference assemblies
                    var refAsms30Dir = Path.Combine(referenceAssembliesPath, "v3.0");
                    if (Directory.Exists(refAsms30Dir))
                    {
                        searchPaths.Add(refAsms30Dir);
                    }
                }

                // .NET 2.0 reference assemblies go last
                searchPaths.Add(net20Dir);

                frameworkInfo.Exists = true;
                frameworkInfo.Path = searchPaths.First();
                frameworkInfo.SearchPaths = searchPaths;

                // Load the redist list in reverse order (most general -> most specific)
                for (int i = searchPaths.Count - 1; i >= 0; i--)
                {
                    var dir = new DirectoryInfo(searchPaths[i]);
                    if (dir.Exists)
                    {
                        PopulateFromRedistList(dir, frameworkInfo);
                    }
                }
            }
            else
            {
                // We don't support building for net20-net35 on Mono
                frameworkInfo.Exists = false;
            }

            if (string.IsNullOrEmpty(frameworkInfo.Name))
            {
                frameworkInfo.Name = SynthesizeFrameworkFriendlyName(targetFramework);
            }
            return frameworkInfo;
        }

        private static string SynthesizeFrameworkFriendlyName(FrameworkName targetFramework)
        {
            // Names are not present in the RedistList.xml file for older frameworks or on Mono
            // We do some custom version string rendering to match how net40 is rendered (.NET Framework 4)
            if (targetFramework.Identifier.Equals(VersionUtility.NetFrameworkIdentifier))
            {
                string versionString = targetFramework.Version.Minor == 0 ?
                    targetFramework.Version.Major.ToString() :
                    targetFramework.Version.ToString();
                return ".NET Framework " + versionString;
            }
            return targetFramework.ToString();
        }

        private static FrameworkInformation GetFrameworkInformation(DirectoryInfo directory, FrameworkName targetFramework)
        {
            var frameworkInfo = new FrameworkInformation();
            frameworkInfo.Exists = true;
            frameworkInfo.Path = directory.FullName;
            frameworkInfo.SearchPaths = new[] {
                frameworkInfo.Path,
                Path.Combine(frameworkInfo.Path, "Facades")
            };

            PopulateFromRedistList(directory, frameworkInfo);
            if (string.IsNullOrEmpty(frameworkInfo.Name))
            {
                frameworkInfo.Name = SynthesizeFrameworkFriendlyName(targetFramework);
            }
            return frameworkInfo;
        }

        private static void PopulateFromRedistList(DirectoryInfo directory, FrameworkInformation frameworkInfo)
        {
            // The redist list contains the list of assemblies for this target framework
            string redistList = Path.Combine(directory.FullName, "RedistList", "FrameworkList.xml");

            if (File.Exists(redistList))
            {
                frameworkInfo.RedistListPath = redistList;

                using (var stream = File.OpenRead(redistList))
                {
                    var frameworkList = XDocument.Load(stream);

                    // On mono, the RedistList.xml has an entry pointing to the TargetFrameworkDirectory
                    // It basically uses the GAC as the reference assemblies for all .NET framework
                    // profiles
                    var targetFrameworkDirectory = frameworkList.Root.Attribute("TargetFrameworkDirectory")?.Value;

                    if (!string.IsNullOrEmpty(targetFrameworkDirectory))
                    {
                        // For some odd reason, the paths are actually listed as \ so normalize them here
                        targetFrameworkDirectory = targetFrameworkDirectory.Replace('\\', Path.DirectorySeparatorChar);

                        // The specified path is the relative path from the RedistList.xml itself
                        var resovledPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(redistList), targetFrameworkDirectory));

                        // Update the path to the framework
                        frameworkInfo.Path = resovledPath;

                        PopulateAssemblies(frameworkInfo.Assemblies, resovledPath);
                        PopulateAssemblies(frameworkInfo.Assemblies, Path.Combine(resovledPath, "Facades"));
                    }
                    else
                    {
                        foreach (var e in frameworkList.Root.Elements())
                        {
                            var assemblyName = e.Attribute("AssemblyName").Value;
                            var version = e.Attribute("Version")?.Value;

                            var entry = new AssemblyEntry();
                            entry.Version = version != null ? Version.Parse(version) : null;
                            frameworkInfo.Assemblies[assemblyName] = entry;
                        }
                    }

                    var nameAttribute = frameworkList.Root.Attribute("Name");

                    frameworkInfo.Name = nameAttribute == null ? null : nameAttribute.Value;
                }
            }
        }

        private static void PopulateAssemblies(IDictionary<string, AssemblyEntry> assemblies, string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            foreach (var assemblyPath in Directory.GetFiles(path, "*.dll"))
            {
                var name = Path.GetFileNameWithoutExtension(assemblyPath);
                var entry = new AssemblyEntry();
                entry.Path = assemblyPath;
                assemblies[name] = entry;
            }
        }

        private static string GetAssemblyPath(IEnumerable<string> basePaths, string assemblyName)
        {
            foreach (var basePath in basePaths)
            {
                var assemblyPath = Path.Combine(basePath, assemblyName + ".dll");

                if (File.Exists(assemblyPath))
                {
                    return assemblyPath;
                }
            }

            return null;
        }
    }
}
