// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation.CSharp;
using Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages;

namespace Microsoft.Dnx.DesignTimeHost.InternalModels
{
    // Represents a project that should be used for intellisense
    internal class ProjectInfo
    {
        public string Path { get; set; }

        public string Configuration { get; set; }

        public FrameworkName FrameworkName { get; set; }

        public FrameworkData TargetFramework { get; set; }

        public CompilationSettings CompilationSettings { get; set; }

        public List<string> SourceFiles { get; set; }

        public DependencyInfo DependencyInfo { get; set; }
    }
}
