using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Microsoft.Net.Runtime
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
            string defaultPath = Path.Combine(
                Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%"),
                "Reference Assemblies", "Microsoft", "Framework");

            PopulateReferenceAssemblies(defaultPath);
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
                foreach (var assemblyName in GetFrameworkAssemblies(redistList))
                {
                    var assemblyPath = Path.Combine(directory.FullName, assemblyName + ".dll");
                    var facadePath = Path.Combine(directory.FullName, "Facades", assemblyName + ".dll");

                    if (File.Exists(assemblyPath))
                    {
                        frameworkInfo.Assemblies.Add(assemblyName, assemblyPath);
                    }
                    else if (File.Exists(facadePath))
                    {
                        frameworkInfo.Assemblies.Add(assemblyName, facadePath);
                    }
                }
            }

            _cache[frameworkName] = frameworkInfo;
        }

        private static IEnumerable<string> GetFrameworkAssemblies(string path)
        {
            if (!File.Exists(path))
            {
                yield break;
            }

            using (var stream = File.OpenRead(path))
            {
                var frameworkList = XDocument.Load(stream);

                foreach (var e in frameworkList.Root.Elements())
                {
                    yield return e.Attribute("AssemblyName").Value;
                }
            }
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