// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime.DependencyManagement;

namespace Microsoft.Dnx.Tooling
{
    public static class CompatibilityChecker
    {
        private const string _errorTemplate =
            "{0} {1} provides a compile-time reference assembly for {2} on {3}, but there is no compatible run-time assembly.";

        public static IEnumerable<string> Check(LockFile lockFile)
        {
            return lockFile.Targets.SelectMany(t => CheckTarget(t));
        }

        private static IEnumerable<string> CheckTarget(LockFileTarget target)
        {
            // Check for matching ref/libs
            var runtimeAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var compileAssemblies = new Dictionary<string, LockFileTargetLibrary>(StringComparer.OrdinalIgnoreCase);
            foreach (var library in target.Libraries)
            {
                // Scan the package for ref assemblies
                foreach (var compile in library.CompileTimeAssemblies
                    .Where(p => p.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                {
                    var name = Path.GetFileNameWithoutExtension(compile.Path);

                    // If we haven't already started tracking this compile-time assembly, AND there isn't already a runtime-loadable version
                    if (!compileAssemblies.ContainsKey(name) && !runtimeAssemblies.Contains(name))
                    {
                        // Track this assembly as potentially compile-time-only
                        compileAssemblies.Add(name, library);
                    }
                }

                // Match up runtime assemblies
                foreach (var runtime in library.RuntimeAssemblies
                    .Where(p => p.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                {
                    var name = Path.GetFileNameWithoutExtension(runtime.Path);

                    // Fix for NuGet/Home#752 - Consider ".ni.dll" (native image/ngen) files matches for ref/ assemblies
                    if (name.EndsWith(".ni"))
                    {
                        name = name.Substring(0, name.Length - 3);
                    }

                    // If there was a compile-time-only assembly under this name...
                    if (compileAssemblies.ContainsKey(name))
                    {
                        // Remove it, we've found a matching runtime ref
                        compileAssemblies.Remove(name);
                    }

                    // Track this assembly as having a runtime assembly
                    runtimeAssemblies.Add(name);
                }
            }

            // Generate errors for un-matched reference assemblies
            foreach (var compile in compileAssemblies)
            {
                var library = compile.Value;
                yield return string.Format(
                    _errorTemplate,
                    library.Name.Red().Bold(),
                    library.Version.ToString().Red().Bold(),
                    compile.Key.Red().Bold(),
                    target.TargetFramework.ToString().Red().Bold());
            }
        }
    }
}
