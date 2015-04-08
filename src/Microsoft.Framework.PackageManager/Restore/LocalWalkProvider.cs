// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.PackageManager.Restore.RuntimeModel;
using NuGet.Frameworks;
using IDependencyProvider = NuGet.DependencyResolver.IDependencyProvider;
using LibraryRange = NuGet.LibraryModel.LibraryRange;
using LibraryDependency = NuGet.LibraryModel.LibraryDependency;

namespace Microsoft.Framework.PackageManager
{
    public class LocalWalkProvider : IWalkProvider
    {
        private readonly IDependencyProvider _dependencyProvider;

        public LocalWalkProvider(IDependencyProvider dependencyProvider)
        {
            _dependencyProvider = dependencyProvider;
        }

        public bool IsHttp { get; private set; }

        public Task<WalkProviderMatch> FindLibrary(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            var description = _dependencyProvider.GetDescription(libraryRange, targetFramework);

            if (description == null)
            {
                return Task.FromResult<WalkProviderMatch>(null);
            }

            return Task.FromResult(new WalkProviderMatch
            {
                Library = description,
                Path = description.Path,
                Provider = this,
            });
        }

        public Task<IEnumerable<LibraryDependency>> GetDependencies(WalkProviderMatch match, NuGetFramework targetFramework)
        {
            var description = _dependencyProvider.GetDescription(match.Library.LibraryRange, targetFramework);

            return Task.FromResult(description.Dependencies);
        }

        public Task<RuntimeFile> GetRuntimes(WalkProviderMatch match, NuGetFramework targetFramework)
        {
            foreach(var path in _dependencyProvider.GetAttemptedPaths(targetFramework))
            {
                var runtimeJsonPath = path
                    .Replace("{name}.nuspec", "runtime.json")
                    .Replace("project.json", "runtime.json")
                    .Replace("{name}", match.Library.Identity.Name)
                    .Replace("{version}", match.Library.Identity.Version.ToString());

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
