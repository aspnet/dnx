// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class UnresolvedDependencyProvider : IDependencyProvider
    {
        public IEnumerable<LibraryDescription> UnresolvedDependencies { get; private set; }

        public IEnumerable<IDependencyProvider> AttemptedProviders { get; set; }

        public UnresolvedDependencyProvider()
        {
            UnresolvedDependencies = Enumerable.Empty<LibraryDescription>();
        }

        public LibraryDescription GetDescription(string name, SemanticVersion version, FrameworkName targetFramework)
        {
            return new LibraryDescription
            {
                Identity = new Library { Name = name, Version = version },
                Dependencies = Enumerable.Empty<Library>()
            };
        }

        public void Initialize(IEnumerable<LibraryDescription> dependencies, FrameworkName targetFramework)
        {
            UnresolvedDependencies = dependencies;
        }

        public IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            return AttemptedProviders.Where(p => p != this)
                                     .SelectMany(p => p.GetAttemptedPaths(targetFramework));
        }

        public string GetMissingDependenciesWarning(FrameworkName targetFramework)
        {
            var sb = new StringBuilder();

            // TODO: Localize messages

            sb.AppendFormat("Failed to resolve the following dependencies for target framework '{0}':", targetFramework.ToString());
            sb.AppendLine();

            foreach (var d in UnresolvedDependencies.OrderBy(d => d.Identity.Name))
            {
                sb.AppendLine("   " + d.Identity.ToString());
            }

            sb.AppendLine();
            sb.AppendLine("Searched Locations:");

            foreach (var path in GetAttemptedPaths(targetFramework))
            {
                sb.AppendLine("  " + path);
            }

            sb.AppendLine();
            sb.AppendLine("Try running 'kpm restore'.");

            return sb.ToString();
        }
    }
}