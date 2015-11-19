// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Runtime
{
    public class ProjectDescription : LibraryDescription
    {
        /// <summary>
        /// Constructs a unresolved project description
        /// </summary>
        public ProjectDescription(string name, string path)
            : base(new LibraryRange(name, frameworkReference: false),
                   new LibraryIdentity(name, new NuGet.SemanticVersion("1.0.0"), isGacOrFrameworkReference: false),
                   path: path,
                   type: LibraryTypes.Project,
                   dependencies: Enumerable.Empty<LibraryDependency>(),
                   assemblies: Enumerable.Empty<string>(),
                   framework: null)
        {
            Project = null;
            Resolved = false;
            Compatible = false;
            TargetFrameworkInfo = null;
        }

        public ProjectDescription(
            LibraryRange libraryRange,
            Project project,
            IEnumerable<LibraryDependency> dependencies,
            IEnumerable<string> assemblies,
            TargetFrameworkInformation targetFrameworkInfo,
            bool resolved) :
                base(
                    libraryRange,
                    new LibraryIdentity(project.Name, project.Version, isGacOrFrameworkReference: false),
                    project.ProjectFilePath,
                    LibraryTypes.Project,
                    dependencies,
                    assemblies,
                    targetFrameworkInfo.FrameworkName)
        {
            Project = project;
            Resolved = resolved;
            Compatible = resolved;
            TargetFrameworkInfo = targetFrameworkInfo;
        }

        public Project Project { get; }

        public TargetFrameworkInformation TargetFrameworkInfo { get; }
    }
}
