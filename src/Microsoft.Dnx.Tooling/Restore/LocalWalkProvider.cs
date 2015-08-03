// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Tooling.Restore.RuntimeModel;

namespace Microsoft.Dnx.Tooling
{
    public class LocalWalkProvider : IWalkProvider
    {
        private readonly IDependencyProvider _dependencyProvider;

        public LocalWalkProvider(IDependencyProvider dependencyProvider)
        {
            _dependencyProvider = dependencyProvider;
        }

        public bool IsHttp { get; private set; }

        public Task<WalkProviderMatch> FindLibrary(LibraryRange libraryRange, FrameworkName targetFramework, bool includeUnlisted)
        {
            var description = _dependencyProvider.GetDescription(libraryRange, targetFramework);

            if (description == null)
            {
                return Task.FromResult<WalkProviderMatch>(null);
            }

            return Task.FromResult(new WalkProviderMatch
            {
                Library = description.Identity,
                Path = description.Path,
                Provider = this,
            });
        }

        public Task<IEnumerable<LibraryDependency>> GetDependencies(WalkProviderMatch match, FrameworkName targetFramework)
        {
            var description = _dependencyProvider.GetDescription(match.Library, targetFramework);

            return Task.FromResult(description.Dependencies);
        }

        public Task<RuntimeFile> GetRuntimes(WalkProviderMatch match, FrameworkName targetFramework)
        {
            foreach(var path in _dependencyProvider.GetAttemptedPaths(targetFramework))
            {
                var runtimeJsonPath = path
                    .Replace("{name}.nuspec", "runtime.json")
                    .Replace("project.json", "runtime.json")
                    .Replace("{name}", match.Library.Name)
                    .Replace("{version}", match.Library.Version.ToString());

                // Console.WriteLine("*** {0}", runtimeJsonPath);
                if (File.Exists(runtimeJsonPath))
                {
                    Console.WriteLine("*** READING {0}", runtimeJsonPath);
                    var formatter = new RuntimeFileFormatter();
                    return Task.FromResult(formatter.ReadRuntimeFile(runtimeJsonPath));
                }
            }
            return Task.FromResult<RuntimeFile>(null);
        }

        public Task CopyToAsync(WalkProviderMatch match, Stream stream)
        {
            // We never call this on local providers
            throw new NotImplementedException();
        }
    }
}
