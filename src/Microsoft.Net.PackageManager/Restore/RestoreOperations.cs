using Microsoft.Net.Runtime;
using NuGet;
using NuGet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Microsoft.Net.PackageManager
{
    public class RestoreOperations
    {
        public async Task CreateGraph(RestoreContext context, Library library)
        {
            var match = await FindLibrary(context, library);
            var dependencies = await match.Provider.GetDependencies(match.Library, context.TargetFrameworkConfiguration.FrameworkName);
            foreach (var dependency in dependencies)
            {
                await CreateGraph(context, dependency);
            }
        }

        public async Task<WalkProviderMatch> FindLibrary(RestoreContext context, Library library)
        {
            var projectMatch = await FindLibraryByName(context, library.Name, context.ProjectLibraryProviders);
            if (projectMatch != null)
            {
                return projectMatch;
            }

            if (library.Version.IsSnapshot)
            {
                var remoteMatch = await FindLibraryBySnapshot(context, library, context.RemoteLibraryProviders);
                if (remoteMatch != null)
                {
                    var localMatch = await FindLibraryByVersion(context, remoteMatch.Library, context.LocalLibraryProviders);
                    if (localMatch != null)
                    {
                        return localMatch;
                    }
                    return remoteMatch;
                }
            }
            else
            {
                var localMatch = await FindLibraryByVersion(context, library, context.LocalLibraryProviders);
                if (localMatch != null)
                {
                    return localMatch;
                }

                var remoteMatch = await FindLibraryByVersion(context, library, context.RemoteLibraryProviders);
                if (remoteMatch != null)
                {
                    return remoteMatch;
                }
            }
            return null;
        }

        public async Task<WalkProviderMatch> FindLibraryByName(RestoreContext context, string name, IEnumerable<IWalkProvider> providers)
        {
            foreach (var provider in providers)
            {
                var match = await provider.FindLibraryByName(name, context.TargetFrameworkConfiguration.FrameworkName);
                if (match != null)
                {
                    return match;
                }
            }
            return null;
        }

        private async Task<WalkProviderMatch> FindLibraryBySnapshot(RestoreContext context, Library library, IEnumerable<IWalkProvider> providers)
        {
            List<Task<WalkProviderMatch>> tasks = new List<Task<WalkProviderMatch>>();
            foreach (var provider in providers)
            {
                tasks.Add(provider.FindLibraryBySnapshot(library, context.TargetFrameworkConfiguration.FrameworkName));
            }
            var matches = await Task.WhenAll(tasks);
            WalkProviderMatch bestMatch = null;
            foreach (var match in matches)
            {
                if (match != null)
                {
                    if (bestMatch == null ||
                        bestMatch.Library.Version < match.Library.Version)
                    {
                        bestMatch = match;
                    }
                }
            }
            return bestMatch;
        }

        private async Task<WalkProviderMatch> FindLibraryByVersion(RestoreContext context, Library library, IEnumerable<IWalkProvider> providers)
        {
            List<Task<WalkProviderMatch>> tasks = new List<Task<WalkProviderMatch>>();
            foreach (var provider in context.RemoteLibraryProviders)
            {
                tasks.Add(provider.FindLibraryByVersion(library, context.TargetFrameworkConfiguration.FrameworkName));
            }
            var matches = await Task.WhenAll(tasks);
            WalkProviderMatch bestMatch = null;
            foreach (var match in matches)
            {
                if (match != null)
                {
                    if (bestMatch == null ||
                        bestMatch.Library.Version < match.Library.Version)
                    {
                        bestMatch = match;
                    }
                }
            }
            return bestMatch;
        }
    }

}