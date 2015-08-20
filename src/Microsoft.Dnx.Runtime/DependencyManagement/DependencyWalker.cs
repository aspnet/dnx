// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    internal static class DependencyWalker
    {
        public static IList<LibraryDescription> Walk(IList<IDependencyProvider> providers, string name, SemanticVersion version, FrameworkName targetFramework)
        {
            var libraries = new List<LibraryDescription>();

            var sw = Stopwatch.StartNew();
            Logger.TraceInformation($"[{nameof(DependencyWalker)}]: Walking dependency graph for '{name} {targetFramework}'.");

            var context = new WalkContext();

            var walkSw = Stopwatch.StartNew();

            context.Walk(
                providers,
                name,
                version,
                targetFramework);

            walkSw.Stop();
            Logger.TraceInformation($"[{nameof(DependencyWalker)}]: Graph walk took {walkSw.ElapsedMilliseconds}ms.");

            context.Populate(targetFramework, libraries);

            sw.Stop();
            Logger.TraceInformation($"$[{ nameof(DependencyWalker)}]: Resolved dependencies for {name} in { sw.ElapsedMilliseconds}ms");

            return libraries;
        }
    }
}
