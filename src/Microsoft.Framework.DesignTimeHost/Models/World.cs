// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages;

namespace Microsoft.Framework.DesignTimeHost.Models
{
    public class World
    {
        public ProjectMessage ProjectInformation { get; set; }
        public Dictionary<FrameworkName, ProjectWorld> Projects { get; set; }

        public World()
        {
            Projects = new Dictionary<FrameworkName, ProjectWorld>();
        }
    }

    public class ProjectWorld
    {
        public CompilationOptionsMessage CompilerOptions { get; set; }
        public SourcesMessage Sources { get; set; }
        public ReferencesMessage References { get; set; }
        public DependenciesMessage Dependencies { get; set; }
        public DiagnosticsMessage Diagnostics { get; set; }
        public CompileMessage Compiled { get; set; }
    }
}
