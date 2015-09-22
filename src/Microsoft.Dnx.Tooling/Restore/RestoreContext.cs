// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Tooling.Restore.RuntimeModel;

namespace Microsoft.Dnx.Tooling
{
    public class RestoreContext
    {
        private Dictionary<string, DependencySpec> _runtimeDependencies;
        public RestoreContext()
        {
            GraphItemCache = new Dictionary<LibraryRange, Task<GraphItem>>();
            MatchCache = new Dictionary<LibraryRange, Task<WalkProviderMatch>>();
        }

        public FrameworkName FrameworkName { get; set; }

        public string RuntimeName { get; set; }
        public IList<RuntimeSpec> RuntimeSpecs { get; set; }

        public IList<IWalkProvider> ProjectLibraryProviders { get; set; }
        public IList<IWalkProvider> LocalLibraryProviders { get; set; }
        public IList<IWalkProvider> RemoteLibraryProviders { get; set; }

        public Dictionary<LibraryRange, Task<GraphItem>> GraphItemCache { get; private set; }
        public Dictionary<LibraryRange, Task<WalkProviderMatch>> MatchCache { get; set; }

        public Dictionary<string, DependencySpec> RuntimeDependencies
        {
            get
            {
                if (_runtimeDependencies == null)
                {
                    if (string.IsNullOrEmpty(RuntimeName) || !RuntimeSpecs.Any())
                    {
                        _runtimeDependencies = new Dictionary<string, DependencySpec>();
                    }
                    else
                    {
                        var dict = new Dictionary<string, DependencySpec>();
                        foreach(var runtimeSpec in RuntimeSpecs)
                        {
                            foreach(var dep in runtimeSpec.Dependencies.Values)
                            {
                                // Specs are ordered from most specific to least specific
                                // So if we already have a match for this package, ignore further matches
                                if (!dict.ContainsKey(dep.Name))
                                {
                                    dict[dep.Name] = dep;
                                }
                            }
                        }
                        _runtimeDependencies = dict;
                    }
                }
                return _runtimeDependencies;
            }
        }
    }
}
