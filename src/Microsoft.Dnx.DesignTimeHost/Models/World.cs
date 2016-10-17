// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.Dnx.DesignTimeHost.Models
{
    public class World
    {
        public Project Project { get; set; }

        public ProjectMessage ProjectInformation { get; set; }

        public DiagnosticsListMessage ProjectDiagnostics { get; set; }

        public Dictionary<FrameworkName, ProjectWorld> Projects { get; set; }

        public ErrorMessage GlobalErrorMessage { get; set; }

        public World()
        {
            Projects = new Dictionary<FrameworkName, ProjectWorld>();
            GlobalErrorMessage = new ErrorMessage();
        }
    }

    public class ProjectWorld
    {
        // State
        public FrameworkName TargetFramework { get; set; }
        public CompilationOptionsMessage CompilerOptions { get; set; }
        public SourcesMessage Sources { get; set; }
        public ReferencesMessage References { get; set; }
        public DependenciesMessage Dependencies { get; set; }
        public DiagnosticsListMessage DependencyDiagnostics { get; set; }
        public DiagnosticsListMessage CompilationDiagnostics { get; set; }
        public OutputsMessage Outputs { get; set; }
    }
}
