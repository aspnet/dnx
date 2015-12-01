// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.CompilationAbstractions;

namespace Microsoft.Dnx.DesignTimeHost.InternalModels
{
    internal class ProjectState
    {
        public string Name { get; set; }

        public List<string> ProjectSearchPaths { get; set; }

        public string GlobalJsonPath { get; set; }

        public List<string> Configurations { get; set; }

        public List<FrameworkData> Frameworks { get; set; }

        public IDictionary<string, string> Commands { get; set; }

        public List<ProjectInfo> Projects { get; set; }

        public List<DiagnosticMessage> Diagnostics { get; set; }

        public Project Project { get; set; }
    }
}
