// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime.Internal;
using System.Diagnostics;

namespace Microsoft.Framework.Runtime.Dependencies
{
    public class FrameworkReferenceResolver : IFrameworkReferenceResolver
    {
        private readonly ILogger Log;
        private readonly IDictionary<NuGetFramework, FrameworkInformation> _cache = new Dictionary<NuGetFramework, FrameworkInformation>();

        private static readonly ISet<string> _desktopFrameworkNames = new HashSet<string>()
        {
            FrameworkConstants.FrameworkIdentifiers.Net,
            FrameworkConstants.FrameworkIdentifiers.Dnx
        };

        public FrameworkReferenceResolver()
        {
            Log = RuntimeLogging.Logger<FrameworkReferenceResolver>();
        }

        public bool TryGetAssembly(string name, NuGetFramework targetFramework, out string path, out NuGetVersion version)
        {
            path = null;
            version = null;

            // Rewrite DNX framework names.
            // DNX versions match 1:1 with .NET Framework versions
            if (string.Equals(targetFramework.Framework, FrameworkConstants.FrameworkIdentifiers.Dnx))
            {
                targetFramework = new NuGetFramework(
                    FrameworkConstants.FrameworkIdentifiers.Net,
                    targetFramework.Version);
            }

            var information = _cache.GetOrAdd(targetFramework, GetFrameworkInformation);

            if (information == null || !information.Exists)
            {
                Log.LogWarning($"No framework information found for {targetFramework}");
                return false;
            }

            lock (information.Assemblies)
            {
                AssemblyEntry entry;
                if (information.Assemblies.TryGetValue(name, out entry))
                {
                    if (string.IsNullOrEmpty(entry.Path))
                    {
                        entry.Path = GetAssemblyPath(information.Path, name);
                    }

                    if (!string.IsNullOrEmpty(entry.Path) && entry.Version == null)
                    {
                        // This code path should only run on mono
                        entry.Version = AssemblyUtils.GetAssemblyVersion(entry.Path);
                    }

                    path = entry.Path;
                    version = new NuGetVersion(entry.Version);

                    if (Log.IsEnabled(LogLevel.Verbose))
                    {
                        // Trim back the path
                        Log.LogVerbose($"Resolved {name} {version} to {path.Substring(information.Path.Length + 1)}");
                    }
                }
            }

            return !string.IsNullOrEmpty(path);
        }

        public string GetFriendlyNuGetFramework(NuGetFramework targetFramework)
        {
            // We don't have a friendly name for this anywhere on the machine so hard code it
            string friendlyName = targetFramework.Framework;
            if (Equals(targetFramework.Framework, FrameworkConstants.CommonFrameworks.DnxCore))
            {
                return "DNX Core " + targetFramework.Version.ToString();
            }
            else if (Equals(targetFramework.Framework, FrameworkConstants.CommonFrameworks.Dnx))
            {
                return "DNX " + targetFramework.Version.ToString();
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

            if (information == null || !information.Exists)
            {
                return null;
            }

            return information.Path;
        }

        public string GetFrameworkRedistListPath(NuGetFramework targetFramework)
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
            if (PlatformHelper.IsMono)
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

        
        private FrameworkInformation GetFrameworkInformation(NuGetFramework targetFramework)
        {
            string referenceAssembliesPath = GetReferenceAssembliesPath();

            if (string.IsNullOrEmpty(referenceAssembliesPath))
            {
                return null;
            }

            // Skip this on mono since it has a slightly different set of reference assemblies at a different
            // location
            FrameworkInformation frameworkInfo;
            if (!PlatformHelper.IsMono && FrameworkDefinitions.TryPopulateFrameworkFastPath(targetFramework.Framework, targetFramework.Version, referenceAssembliesPath, out frameworkInfo))
            {
                return frameworkInfo;
            }

            // Identify the .NET Framework related to this DNX framework
            if (_desktopFrameworkNames.Contains(targetFramework.Framework))
            {
                // Rewrite the name from DNX -> NET (unless of course the incoming
                // name was NET, in which case we rewrite from NET -> NET which is harmless)
                return GetFrameworkInformation(
                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Net, targetFramework.Version));
            }

            return null;
        }

        private FrameworkInformation GetFrameworkInformation(NuGetFramework targetFramework, string referenceAssembliesPath)
        {
            var basePath = Path.Combine(referenceAssembliesPath,
                                        targetFramework.Framework,
                                        "v" + GetVersionFolderName(targetFramework.Version));

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

        // NuGetVersion.ToString() doesn't render the way we want.
        // .NET Framework folder names are ALWAYS 2-3 digits long, but
        // NuGetVersion will render the third and fourth digits as
        // zero when they are not present. For example, NuGetVersion
        // will render "v4.5.1.0" instead of "v4.5.1" (the actual folder name)
        private static string GetVersionFolderName(Version version)
        {
            var folderName = $"{version.Major}.{version.Minor}";
            if (version.Revision > 0)
            {
                folderName += $".{version.Revision}";
            }
            return folderName;
        }

        private FrameworkInformation GetFrameworkInformation(DirectoryInfo directory, NuGetFramework targetFramework)
        {
            var frameworkInfo = new FrameworkInformation();
            frameworkInfo.Path = directory.FullName;

            // The redist list contains the list of assemblies for this target framework
            string redistList = Path.Combine(directory.FullName, "RedistList", "FrameworkList.xml");

            Log.LogVerbose($"Loading Framework Information for {targetFramework}");
            Log.LogVerbose($"Scanning {directory.FullName}");

            if (File.Exists(redistList))
            {
                if (Log.IsEnabled(LogLevel.Verbose))
                {
                    Log.LogVerbose($"Reading redist list from {redistList.Substring(directory.FullName.Length + 1)}");
                }
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
                        Log.LogDebug($"Found TargetFrameworkDirectory={targetFrameworkDirectory} in redist list");

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
                            frameworkInfo.Assemblies.Add(assemblyName, entry);
                            Log.LogDebug($"Found assembly {assemblyName} {entry.Version}, in redist list");
                        }
                    }
                    var nameAttribute = frameworkList.Root.Attribute("Name");

                    frameworkInfo.Name = nameAttribute == null ? null : nameAttribute.Value;
                }
            }

            return frameworkInfo;
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
    }
}
