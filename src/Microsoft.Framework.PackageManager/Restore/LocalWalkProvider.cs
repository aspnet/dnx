// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.PackageManager.Restore.NuGet;
using Microsoft.Framework.Runtime;
using NuGet;
using NuGet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Microsoft.Framework.PackageManager
{
    public class WalkProviderMatch
    {
        public IWalkProvider Provider { get; set; }
        public Library Library { get; set; }
        public string Path { get; set; }
    }

    public interface IWalkProvider
    {
        Task<WalkProviderMatch> FindLibraryByName(string name, FrameworkName targetFramework);
        Task<WalkProviderMatch> FindLibraryByVersion(Library library, FrameworkName targetFramework);
        Task<WalkProviderMatch> FindLibraryBySnapshot(Library library, FrameworkName targetFramework);
        Task<IEnumerable<Library>> GetDependencies(WalkProviderMatch match, FrameworkName targetFramework);
        Task CopyToAsync(WalkProviderMatch match, Stream stream);
    }

    public class LocalWalkProvider : IWalkProvider
    {
        IDependencyProvider _dependencyProvider;

        public LocalWalkProvider(IDependencyProvider dependencyProvider)
        {
            _dependencyProvider = dependencyProvider;
        }

        public Task<WalkProviderMatch> FindLibraryByName(string name, FrameworkName targetFramework)
        {
            var description = _dependencyProvider.GetDescription(name, new SemanticVersion(new Version(0, 0)), targetFramework);
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
            var description = _dependencyProvider.GetDescription(library.Name, library.Version, targetFramework);
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
            var description = _dependencyProvider.GetDescription(library.Name, library.Version, targetFramework);
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

        public Task<IEnumerable<Library>> GetDependencies(WalkProviderMatch match, FrameworkName targetFramework)
        {
            var description = _dependencyProvider.GetDescription(match.Library.Name, match.Library.Version, targetFramework);
            return Task.FromResult(description.Dependencies);
        }

        public Task CopyToAsync(WalkProviderMatch match, Stream stream)
        {
            throw new NotImplementedException();
        }
    }
}