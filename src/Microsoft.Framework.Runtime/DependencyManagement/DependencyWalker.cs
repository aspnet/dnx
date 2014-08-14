// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class DependencyWalker
    {
        private readonly IEnumerable<IDependencyProvider> _dependencyProviders;
        private readonly List<LibraryDescription> _libraries = new List<LibraryDescription>();

        public DependencyWalker(IEnumerable<IDependencyProvider> dependencyProviders)
        {
            _dependencyProviders = dependencyProviders;
        }

        public IList<LibraryDescription> Libraries
        {
            get { return _libraries; }
        }

        public IEnumerable<IDependencyProvider> DependencyProviders
        {
            get
            {
                return _dependencyProviders;
            }
        }

        public void Walk(string name, SemanticVersion version, string configuration, FrameworkName targetFramework)
        {
            var sw = Stopwatch.StartNew();
            Trace.TraceInformation("[{0}]: Walking dependency graph for '{1} ({2}) {3}'.", GetType().Name, name, configuration, targetFramework);

            var context = new WalkContext();

            context.Walk(
                _dependencyProviders,
                name,
                version,
                configuration,
                targetFramework);

            context.Populate(targetFramework, Libraries);

            sw.Stop();
            Trace.TraceInformation("[{0}]: Resolved dependencies for {1} in {2}ms", GetType().Name, name, sw.ElapsedMilliseconds);
        }
    }
}
