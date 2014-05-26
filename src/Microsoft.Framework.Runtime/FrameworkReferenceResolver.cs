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
    public class FrameworkReferenceResolver : IFrameworkReferenceResolver
    {
        private readonly IDictionary<FrameworkName, FrameworkInformation> _cache = new Dictionary<FrameworkName, FrameworkInformation>();

        public FrameworkReferenceResolver()
        {
            PopulateCache();
        }

        public bool TryGetAssembly(string name, FrameworkName targetFramework, out string path)
        {
            FrameworkInformation frameworkInfo;
            if (_cache.TryGetValue(targetFramework, out frameworkInfo))
            {
                return frameworkInfo.Assemblies.TryGetValue(name, out path);
            }

            path = null;
            return false;
        }

        public string GetFriendlyFrameworkName(FrameworkName targetFramework)
        {
            FrameworkInformation frameworkInfomation;
            if (_cache.TryGetValue(targetFramework, out frameworkInfomation))
            {
                return frameworkInfomation.Name;
            }

            return null;
        }

        public string GetFrameworkPath(FrameworkName targetFramework)
        {
            FrameworkInformation frameworkInfomation;
            if (_cache.TryGetValue(targetFramework, out frameworkInfomation))
            {
                return frameworkInfomation.Path;
            }

            return null;
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
                string referenceAssembliesPath = GetReferenceAssembliesPath();

                if (!string.IsNullOrEmpty(referenceAssembliesPath))
                {
                    PopulateReferenceAssemblies(referenceAssembliesPath);
                }
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
            frameworkInfo.Path = directory.FullName;

            string redistList = Path.Combine(directory.FullName, "RedistList", "FrameworkList.xml");

            if (File.Exists(redistList))
            {
                using (var stream = File.OpenRead(redistList))
                {
                    var frameworkList = XDocument.Load(stream);

                    foreach (var pair in GetFrameworkAssemblies(directory.FullName, frameworkList))
                    {
                        frameworkInfo.Assemblies.Add(pair.Item1, pair.Item2);
                    }

                    var nameAttribute = frameworkList.Root.Attribute("Name");

                    frameworkInfo.Name = nameAttribute == null ? null : nameAttribute.Value;
                }
            }

            _cache[frameworkName] = frameworkInfo;
        }

        private static IEnumerable<Tuple<string, string>> GetFrameworkAssemblies(string frameworkPath, XDocument frameworkList)
        {
            var assemblies = new List<Tuple<string, string>>();

            foreach (var e in frameworkList.Root.Elements())
            {
                string assemblyName = e.Attribute("AssemblyName").Value;
                string assemblyPath = GetAssemblyPath(frameworkPath, assemblyName);

                if (!string.IsNullOrEmpty(assemblyPath))
                {
                    assemblies.Add(Tuple.Create(assemblyName, assemblyPath));
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

            public string Path { get; set; }

            public IDictionary<string, string> Assemblies { get; private set; }

            public string Name { get; set; }
        }
    }
}