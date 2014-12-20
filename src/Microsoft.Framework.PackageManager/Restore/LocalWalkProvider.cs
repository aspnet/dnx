// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
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

        public Task<WalkProviderMatch> FindLibraryByName(string name, FrameworkName targetFramework)
        {
            var library = new Library
            {
                Name = name,
                Version = new SemanticVersion(new Version(0, 0))
            };

            var description = _dependencyProvider.GetDescription(library, targetFramework);

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

        public Task<WalkProviderMatch> FindLibraryByVersion(Library library, FrameworkName targetFramework)
        {
            var description = _dependencyProvider.GetDescription(library, targetFramework);
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

        public Task<WalkProviderMatch> FindLibraryBySnapshot(Library library, FrameworkName targetFramework)
        {
            var description = _dependencyProvider.GetDescription(library, targetFramework);
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

        public Task CopyToAsync(WalkProviderMatch match, Stream stream)
        {
            // We never call this on local providers
            throw new NotImplementedException();
        }
    }
}
