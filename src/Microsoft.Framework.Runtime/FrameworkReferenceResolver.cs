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

namespace Microsoft.Framework.Runtime
{
    public class FrameworkReferenceResolver
    {
        private readonly IDictionary<FrameworkName, FrameworkInformation> _cache = new Dictionary<FrameworkName, FrameworkInformation>();

        public FrameworkReferenceResolver()
        {
            PopulateCache();
        }

        public bool TryGetAssembly(string name, FrameworkName frameworkName, out string path)
        {
            FrameworkInformation frameworkInfo;
            if (_cache.TryGetValue(frameworkName, out frameworkInfo))
            {
                return frameworkInfo.Assemblies.TryGetValue(name, out path);
            }

            path = null;
            return false;
        }

        private void PopulateCache()
        {
            if (PlatformHelper.IsMono)
            {
#if NET45
                var mscorlibLocationOnThisRunningMonoInstance = typeof(object).GetTypeInfo().Assembly.Location;

                var libPath = Path.GetDirectoryName(Path.GetDirectoryName(mscorlibLocationOnThisRunningMonoInstance));

                var supportedVersions = new[] { "4.5", "4.0" };

                foreach (var version in supportedVersions)
                {
                    var targetFrameworkPath = Path.Combine(libPath, version);

                    if (!Directory.Exists(targetFrameworkPath))
                    {
                        continue;
                    }

                    var frameworkName = new FrameworkName(VersionUtility.DefaultTargetFramework.Identifier, new Version(version));

                    var frameworkInfo = new FrameworkInformation();

                    var assemblies = new List<Tuple<string, string>>();

                    PopulateAssemblies(assemblies, targetFrameworkPath);
                    PopulateAssemblies(assemblies, Path.Combine(targetFrameworkPath, "Facades"));

                    foreach (var pair in assemblies)
                    {
                        frameworkInfo.Assemblies.Add(pair.Item1, pair.Item2);
                    }

                    _cache[frameworkName] = frameworkInfo;
                }
#endif
            }
            else
            {
                string defaultPath = Path.Combine(
                    Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%"),
                    "Reference Assemblies", "Microsoft", "Framework");

                PopulateReferenceAssemblies(defaultPath);
            }
        }

        private void PopulateReferenceAssemblies(string path)
        {
            var di = new DirectoryInfo(path);

            if (!di.Exists)
            {
                return;
            }

            foreach (var framework in di.EnumerateDirectories())
            {
                if (framework.Name.StartsWith("v"))
                {
                    continue;
                }

                foreach (var version in framework.EnumerateDirectories())
                {
                    var frameworkName = new FrameworkName(framework.Name, new Version(version.Name.TrimStart('v')));

                    PopulateFrameworkReferences(version, frameworkName);

                    var profiles = new DirectoryInfo(Path.Combine(version.FullName, "Profile"));
                    if (profiles.Exists)
                    {
                        foreach (var profile in profiles.EnumerateDirectories("Profile*"))
                        {
                            var profileFrameworkName = new FrameworkName(frameworkName.Identifier, frameworkName.Version, profile.Name);

                            PopulateFrameworkReferences(profile, profileFrameworkName);
                        }
                    }
                }
            }
        }

        private void PopulateFrameworkReferences(DirectoryInfo directory, FrameworkName frameworkName)
        {
            var frameworkInfo = new FrameworkInformation();
            string redistList = Path.Combine(directory.FullName, "RedistList", "FrameworkList.xml");

            if (File.Exists(redistList))
            {
                foreach (var pair in GetFrameworkAssemblies(directory.FullName, redistList))
                {
                    frameworkInfo.Assemblies.Add(pair.Item1, pair.Item2);
                }
            }

            _cache[frameworkName] = frameworkInfo;
        }

        private static IEnumerable<Tuple<string, string>> GetFrameworkAssemblies(string frameworkPath, string path)
        {
            if (!File.Exists(path))
            {
                return Enumerable.Empty<Tuple<string, string>>();
            }

            var assemblies = new List<Tuple<string, string>>();

            using (var stream = File.OpenRead(path))
            {
                var frameworkList = XDocument.Load(stream);

                foreach (var e in frameworkList.Root.Elements())
                {
                    string assemblyName = e.Attribute("AssemblyName").Value;
                    string assemblyPath = GetAssemblyPath(frameworkPath, assemblyName);

                    if (!string.IsNullOrEmpty(assemblyPath))
                    {
                        assemblies.Add(Tuple.Create(assemblyName, assemblyPath));
                    }
                }
            }

            return assemblies;
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
            var facadePath = Path.Combine(basePath, "Facades", assemblyName + ".dll");

            if (File.Exists(assemblyPath))
            {
                return assemblyPath;
            }
            else if (File.Exists(facadePath))
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

            public IDictionary<string, string> Assemblies { get; private set; }
        }
    }
}