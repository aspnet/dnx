// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime.DependencyManagement;

namespace Microsoft.Dnx.Runtime
{
    public class CompatibilityChecker
    {
        private readonly LockFile _lockFile;
        private readonly Dictionary<string, LockFileLibrary> _lockFileLibraries;
        private readonly Dictionary<FrameworkName, HashSet<string>> _frameworkRuntimeAssemblies;

        public CompatibilityChecker(LockFile lockFile)
        {
            _lockFile = lockFile;
            _lockFileLibraries = _lockFile.Libraries.ToDictionary(l => l.Name);
            _frameworkRuntimeAssemblies = lockFile.Targets.ToDictionary(
                keySelector: t => t.TargetFramework,
                elementSelector: GetRuntimeAssemblySet);
        }

        public IEnumerable<CompatibilityIssue> GetAllCompatibilityIssues()
        {
            foreach (var target in _lockFile.Targets)
            {
                foreach (var library in target.Libraries)
                {
                    var issue = CheckTargetLibrary(library, target.TargetFramework);
                    if (issue != null)
                    {
                        yield return issue;
                    }
                }
            }
        }

        public CompatibilityIssue CheckTargetLibrary(LockFileTargetLibrary targetLibrary, FrameworkName framework)
        {
            var lockFileLibrary = _lockFileLibraries[targetLibrary.Name];
            var containsAssembly = lockFileLibrary.Files
                .Any(x => x.StartsWith($"ref{Path.DirectorySeparatorChar}") ||
                    x.StartsWith($"lib{Path.DirectorySeparatorChar}"));
            if (containsAssembly &&
                !targetLibrary.FrameworkAssemblies.Any() &&
                !targetLibrary.CompileTimeAssemblies.Any() &&
                !targetLibrary.RuntimeAssemblies.Any())
            {
                return new CompatibilityIssue
                {
                    LibraryName = lockFileLibrary.Name,
                    LibraryVersion = lockFileLibrary.Version,
                    Framework = framework,
                    Type = CompatibilityIssue.IssueType.UnsupportedFramework
                };
            }

            // Get run-time assemblies provided by current library
            var runtimeAssemblies = _frameworkRuntimeAssemblies[framework];

            // Get compile-time assembies provided by current library
            var compileAssemblies = targetLibrary.CompileTimeAssemblies
                .Where(x => x.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(x => Path.GetFileNameWithoutExtension(x.Path));

            if (!runtimeAssemblies.IsSupersetOf(compileAssemblies))
            {
                // The library provids compile-time reference assembly
                // but there is no compatible run-time assembly provided by any library in dependency closure
                return new CompatibilityIssue
                {
                    LibraryName = lockFileLibrary.Name,
                    LibraryVersion = lockFileLibrary.Version,
                    Framework = framework,
                    Type = CompatibilityIssue.IssueType.MissingRuntimeAssembly
                };
            }

            // There is no compatibility issue found for current library
            return null;
        }

        private static HashSet<string> GetRuntimeAssemblySet(LockFileTarget target)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var runtime in target.Libraries
                .SelectMany(l => l.RuntimeAssemblies)
                .Where(x => x.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                var name = Path.GetFileNameWithoutExtension(runtime.Path);

                // Fix for NuGet/Home#752 - Consider ".ni.dll" (native image/ngen) files matches for ref/ assemblies
                if (name.EndsWith(".ni"))
                {
                    name = name.Substring(0, name.Length - 3);
                }

                // Track this assembly as having a runtime assembly
                set.Add(name);
            }

            return set;
        }
    }
}
