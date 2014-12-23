// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Framework.Project;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager.DependencyAnalyzer
{
    internal class DependencyResolverForCLR : IDependenyResolver
    {
        private IDictionary<string, PackageAssembly> _nugetLookUp;

        public DependencyResolverForCLR(ApplicationHostContext hostContext)
        {
            _nugetLookUp = hostContext.NuGetDependencyProvider.PackageAssemblyLookup;
        }

        public ISet<string> Resolve(string assemblyName)
        {
            var result = new HashSet<string>();
            var stack = new Stack<string>();
            stack.Push(assemblyName);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                PackageAssembly assembly;
                if (_nugetLookUp.TryGetValue(current, out assembly))
                {
                    if (!result.Add(assembly.Path))
                    {
                        continue;
                    }

                    var assemblyInfo = new AssemblyInformation(assembly.Path, null);
                    foreach (var reference in assemblyInfo.GetDependencies())
                    {
                        stack.Push(reference);
                    }
                }
            }

            return result;
        }
    }
}