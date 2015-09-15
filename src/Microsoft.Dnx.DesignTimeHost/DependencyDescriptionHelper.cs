// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.DesignTimeHost
{
    internal static class DependencyDescriptionHelper
    {
        public static DependencyDescription CreateDependencyDescription(LibraryDescription library, int protocolVersion)
        {
            var result = new DependencyDescription
            {
                Name = library.Identity.Name,
                DisplayName = library.Identity.IsGacOrFrameworkReference ? library.RequestedRange.GetReferenceAssemblyName() : library.Identity.Name,
                Version = library.Identity.Version?.ToString(),
                Type = library.Type,
                Resolved = library.Resolved,
                Path = library.Path,
                Dependencies = library.Dependencies.Select(dependency => new DependencyItem
                {
                    Name = dependency.Name,
                    Version = dependency.Library?.Identity?.Version?.ToString()
                })
            };

            if (protocolVersion < 3 && !library.Resolved)
            {
                result.Type = "Unresolved";
            }

            return result;
        }
    }
}
