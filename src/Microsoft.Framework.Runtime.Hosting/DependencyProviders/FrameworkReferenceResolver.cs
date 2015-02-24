// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Xml.Linq;
using NuGet;
using NuGet.Frameworks;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.Runtime.Hosting.DependencyProviders
{
    public class FrameworkReferenceResolver : IFrameworkReferenceResolver
    {
        private readonly IDictionary<NuGetFramework, FrameworkInformation> _cache = new Dictionary<NuGetFramework, FrameworkInformation>();

        private static readonly IDictionary<NuGetFramework, List<NuGetFramework>> _aliases = new Dictionary<NuGetFramework, List<NuGetFramework>>()
        {
            { FrameworkConstants.CommonFrameworks.AspNet50, new List<NuGetFramework> {
                    FrameworkConstants.CommonFrameworks.Net451
                }
            },
        };

        private static readonly IDictionary<NuGetFramework, NuGetFramework> _monoAliases = new Dictionary<NuGetFramework, NuGetFramework>
        {
            { FrameworkConstants.CommonFrameworks.Net451, FrameworkConstants.CommonFrameworks.AspNet50 }
        };

        public FrameworkReferenceResolver()
        {
            PopulateCache();
        }

        public bool TryGetAssembly(string name, NuGetFramework targetFramework, out string path)
        {
            var information = _cache.GetOrAdd(targetFramework, GetFrameworkInformation);

            if (information == null)
            {
                path = null;
                return false;
            }

            lock (information.Assemblies)
            {
                if (information.Assemblies.TryGetValue(name, out path) &&
                    string.IsNullOrEmpty(path))
                {
                    path = GetAssemblyPath(information.Path, name);
                    Logger.TraceInformation($"[FrameworkReferenceResolver] Resolved {name} to {path}");
                    information.Assemblies[name] = path;
                }
            }

            return !string.IsNullOrEmpty(path);
        }

        public string GetFriendlyFrameworkName(NuGetFramework targetFramework)
        {
            // We don't have a friendly name for this anywhere on the machine so hard code it
            if (string.Equals(targetFramework.Framework, "K", StringComparison.OrdinalIgnoreCase))
            {
                return ".NET Core Framework 4.5";
            }
            else if (Equals(targetFramework, FrameworkConstants.CommonFrameworks.AspNetCore50))
            {
                return "ASP.NET Core 5.0";
            }
            else if (Equals(targetFramework, FrameworkConstants.CommonFrameworks.AspNet50))
            {
                return "ASP.NET 5.0";
            }

            var information = _cache.GetOrAdd(targetFramework, GetFrameworkInformation);

            if (information == null)
            {
                return targetFramework.ToString();
            }

            return information.Name;
        }

        public string GetFrameworkPath(NuGetFramework targetFramework)
        {
            var information = _cache.GetOrAdd(targetFramework, GetFrameworkInformation);

            if (information == null)
            {
                return null;
            }

            return information.Path;
        }

        public string GetFrameworkRedistListPath(NuGetFramework targetFramework)
        {
            var information = _cache.GetOrAdd(targetFramework, GetFrameworkInformation);

            if (information == null)
            {
                return null;
            }

            return information.RedistListPath;
        }

        public static string GetReferenceAssembliesPath()
        {
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

        private void PopulateCache()
        {
#if ASPNET50
            if (PlatformHelper.IsMono)
            {
                var mscorlibLocationOnThisRunningMonoInstance = typeof(object).GetTypeInfo().Assembly.Location;

                var libPath = Path.GetDirectoryName(Path.GetDirectoryName(mscorlibLocationOnThisRunningMonoInstance));

                // Mono is a bit inconsistent as .NET 4.5 and .NET 4.5.1 are the
                // same folder
                var supportedVersions = new Dictionary<string, string> {
                    { "4.6", "4.5" },
                    { "4.5.3", "4.5" },
                    { "4.5.1", "4.5" },
                    { "4.5", "4.5" },
                    { "4.0", "4.0" }
                };

                // Temporary cache while enumerating assemblies in directories
                var pathCache = new Dictionary<string, FrameworkInformation>();

                foreach (var versionFolderPair in supportedVersions)
                {
                    var targetFrameworkPath = Path.Combine(libPath, versionFolderPair.Value);

                    if (!Directory.Exists(targetFrameworkPath))
                    {
                        continue;
                    }

                    FrameworkInformation frameworkInfo;
                    if (!pathCache.TryGetValue(targetFrameworkPath, out frameworkInfo))
                    {
                        frameworkInfo = new FrameworkInformation();
                        frameworkInfo.Path = targetFrameworkPath;

                        var assemblies = new List<Tuple<string, string>>();

                        PopulateAssemblies(assemblies, targetFrameworkPath);
                        PopulateAssemblies(assemblies, Path.Combine(targetFrameworkPath, "Facades"));

                        foreach (var pair in assemblies)
                        {
                            frameworkInfo.Assemblies[pair.Item1] = pair.Item2;
                        }

                        pathCache[targetFrameworkPath] = frameworkInfo;
                    }

                    var frameworkName = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Net, new Version(versionFolderPair.Key));
                    _cache[frameworkName] = frameworkInfo;

                    NuGetFramework aliasFrameworkName;
                    if (_monoAliases.TryGetValue(frameworkName, out aliasFrameworkName))
                    {
                        _cache[aliasFrameworkName] = frameworkInfo;
                    }
                }

                // Not needed anymore
                pathCache.Clear();
            }
#endif
        }

        private static FrameworkInformation GetFrameworkInformation(NuGetFramework targetFramework)
        {
            string referenceAssembliesPath = GetReferenceAssembliesPath();

            if (string.IsNullOrEmpty(referenceAssembliesPath))
            {
                return null;
            }

            List<NuGetFramework> candidates;
            if (_aliases.TryGetValue(targetFramework, out candidates))
            {
                foreach (var framework in candidates)
                {
                    var information = GetFrameworkInformation(framework, referenceAssembliesPath);

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

        private static FrameworkInformation GetFrameworkInformation(NuGetFramework targetFramework, string referenceAssembliesPath)
        {
            var basePath = Path.Combine(referenceAssembliesPath,
                                        targetFramework.Framework,
                                        "v" + GetVersionFolderName(targetFramework.Version));

            if (!string.IsNullOrEmpty(targetFramework.Profile))
            {
                basePath = Path.Combine(basePath, "Profile", targetFramework.Profile);
            }

            Logger.TraceInformation($"[FrameworkReferenceResolver] Loading framework info for {targetFramework} from {basePath}");

            var version = new DirectoryInfo(basePath);
            if (!version.Exists)
            {
                Logger.TraceWarning($"[FrameworkReferenceResolver] Unable to find framework info directory {basePath}");
                return null;
            }

            return GetFrameworkInformation(version, targetFramework);
        }

        private static string GetVersionFolderName(Version version)
        {
            var folderName = $"{version.Major}.{version.Minor}";
            if(version.Revision > 0)
            {
                folderName += $".{version.Revision}";
            }
            return folderName;
        }

        private static FrameworkInformation GetFrameworkInformation(DirectoryInfo directory, NuGetFramework targetFramework)
        {
            Logger.TraceInformation($"[FrameworkReferenceResolver] Loading framework info from {directory}");

            var frameworkInfo = new FrameworkInformation();
            frameworkInfo.Path = directory.FullName;

            // The redist list contains the list of assemblies for this target framework
            string redistList = Path.Combine(directory.FullName, "RedistList", "FrameworkList.xml");

            if (File.Exists(redistList))
            {
                frameworkInfo.RedistListPath = redistList;

                using (var stream = File.OpenRead(redistList))
                {
                    var frameworkList = XDocument.Load(stream);

                    foreach (var e in frameworkList.Root.Elements())
                    {
                        string assemblyName = e.Attribute("AssemblyName").Value;
                        frameworkInfo.Assemblies.Add(assemblyName, null);
                    }

                    var nameAttribute = frameworkList.Root.Attribute("Name");

                    frameworkInfo.Name = nameAttribute == null ? null : nameAttribute.Value;
                }
            }

            return frameworkInfo;
        }

        private static void PopulateAssemblies(List<Tuple<string, string>> assemblies, string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            foreach (var assemblyPath in Directory.GetFiles(path, "*.dll"))
            {
                assemblies.Add(Tuple.Create(Path.GetFileNameWithoutExtension(assemblyPath), assemblyPath));
            }
        }

        private static string GetAssemblyPath(string basePath, string assemblyName)
        {
            var assemblyPath = Path.Combine(basePath, assemblyName + ".dll");

            if (File.Exists(assemblyPath))
            {
                return assemblyPath;
            }

            var facadePath = Path.Combine(basePath, "Facades", assemblyName + ".dll");

            if (File.Exists(facadePath))
            {
                return facadePath;
            }

            return null;
        }

        private class FrameworkInformation
        {
            public FrameworkInformation()
            {
                Assemblies = new Dictionary<string, string>();
            }

            public string Path { get; set; }

            public string RedistListPath { get; set; }

            public IDictionary<string, string> Assemblies { get; private set; }

            public string Name { get; set; }
        }
    }
}
