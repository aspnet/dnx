// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Framework.Project;
using Microsoft.Framework.Runtime;
using System.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager.DependencyAnalyzer
{
    internal class DependencyResolverForCoreCLR : IDependenyResolver
    {
        private readonly IDictionary<string, PackageAssembly> _nugetLookUp;
        private readonly string _coreclrFolder;

        public DependencyResolverForCoreCLR(
            ApplicationHostContext hostContext, string coreclrFolder)
        {
            _nugetLookUp = hostContext.NuGetDependencyProvider.PackageAssemblyLookup;
            _coreclrFolder = coreclrFolder;
        }

        public ISet<string> Resolve(string assemblyName)
        {
            var result = new HashSet<string>();

            PackageAssembly assembly;
            if (_nugetLookUp.TryGetValue(assemblyName, out assembly))
            {
                result.AddRange(WalkAll(assembly.Path));
            }
            else
            {
                string coreclrAssemblyPath;
                if (TryFindCoreClrPath(assemblyName, out coreclrAssemblyPath))
                {
                    result.Add(coreclrAssemblyPath);
                }
                else
                {
                    throw new InvalidOperationException("Can't locate dependent assembly: " + assemblyName);
                }
            }

            return result;
        }

        private IList<string> WalkAll(string rootPath)
        {
            var result = new HashSet<string>();
            var stack = new Stack<string>();

            var name = Path.GetFileNameWithoutExtension(rootPath);

            string coreclrAssemblyPath;
            if (TryFindCoreClrPath(name, out coreclrAssemblyPath))
            {
                rootPath = coreclrAssemblyPath;
            }

            stack.Push(rootPath);

            while (stack.Count > 0)
            {
                var path = stack.Pop();

                if (!result.Add(path))
                {
                    continue;
                }

                var assemblyInformation = new AssemblyInformation(path, null);
                foreach (var reference in assemblyInformation.GetDependencies())
                {
                    string newPath;
                    if (TryFindCoreClrPath(reference, out newPath))
                    {
                        stack.Push(newPath);
                    }
                }
            }

            return result.ToList();
        }

        private bool TryFindCoreClrPath(string name, out string filepath)
        {
            filepath = Path.Combine(_coreclrFolder, name + ".dll");
            return File.Exists(filepath);
        }
    }
}