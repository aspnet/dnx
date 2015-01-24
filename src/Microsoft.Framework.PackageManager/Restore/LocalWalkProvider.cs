// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.DependencyManagement;
using NuGet;

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

        public Task<WalkProviderMatch> FindLibrary(LibraryRange libraryRange, FrameworkName targetFramework)
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

        public Task<LockFileLibrary> GetLockFileLibrary(WalkProviderMatch match)
        {
            var nugetProvider = _dependencyProvider as NuGetDependencyResolver;
            if (nugetProvider != null)
            {
                return Task.FromResult(nugetProvider.GetLockFileLibrary(match.Library));
            }
            return Task.FromResult(default(LockFileLibrary));
        }


        public Task CopyToAsync(WalkProviderMatch match, Stream stream)
        {
            // We never call this on local providers
            throw new NotImplementedException();
        }
    }
}
