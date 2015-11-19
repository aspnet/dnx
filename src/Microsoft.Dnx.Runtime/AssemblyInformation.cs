// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;

namespace Microsoft.Dnx.Runtime
{
    public class AssemblyInformation
    {
        public static readonly IEqualityComparer<AssemblyInformation> NameComparer = new AssemblyNameComparer();

        public AssemblyInformation(string path, string processorArchitecture)
        {
            path = Path.GetFullPath(path);
            Name = Path.GetFileNameWithoutExtension(path);

            AssemblyPath = path;
            ProcessorArchitecture = processorArchitecture;
        }

        public bool IsRuntimeAssembly { get; set; }

        public string Name { get; private set; }

        public string AssemblyPath { get; private set; }

        public string ProcessorArchitecture { get; private set; }

        public string NativeImageDirectory
        {
            get
            {
                var assemblyDirectory = Path.GetDirectoryName(AssemblyPath);
                if (IsRuntimeAssembly)
                {
                    return assemblyDirectory;
                }

                return Path.Combine(assemblyDirectory, ProcessorArchitecture);
            }
        }

        public string NativeImagePath
        {
            get
            {
                return Path.Combine(NativeImageDirectory, Name + ".ni.dll");
            }
        }
        
        public string NativePdbPath
        {
            get
            {
                return Path.Combine(NativeImageDirectory, Name + ".ni.pdb");
            }
        }

        public IEnumerable<AssemblyInformation> Closure { get; set; }

        public bool Generated { get; set; }

        public ICollection<string> GetDependencies()
        {
            var dependencies = new HashSet<string>();

            using (var stream = File.OpenRead(AssemblyPath))
            using (var peReader = new PEReader(stream))
            {
                var reader = peReader.GetMetadataReader();

                foreach (var a in reader.AssemblyReferences)
                {
                    var reference = reader.GetAssemblyReference(a);
                    var referenceName = reader.GetString(reference.Name);

                    dependencies.Add(referenceName);
                }
            }

            return dependencies;
        }

        public static bool IsValidImage(string path)
        {
            // Skip native images
            if (path.EndsWith(".ni.dll", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            using (var stream = File.OpenRead(path))
            using (var peReader = new PEReader(stream))
            {
                return peReader.HasMetadata;
            }
        }

        public override bool Equals(object obj)
        {
            return ((AssemblyInformation)obj).AssemblyPath.Equals(AssemblyPath, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return AssemblyPath.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
        }

        private class AssemblyNameComparer : IEqualityComparer<AssemblyInformation>
        {
            public bool Equals(AssemblyInformation x, AssemblyInformation y)
            {
                return x.Name.Equals(y.Name, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(AssemblyInformation obj)
            {
                return obj.Name.ToLowerInvariant().GetHashCode();
            }
        }
    }
}
