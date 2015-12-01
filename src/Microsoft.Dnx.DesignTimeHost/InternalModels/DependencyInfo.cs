// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages;
using Microsoft.Extensions.CompilationAbstractions;

namespace Microsoft.Dnx.DesignTimeHost.InternalModels
{
    internal class DependencyInfo
    {
        public List<DiagnosticMessage> Diagnostics { get; set; }

        public Dictionary<string, byte[]> RawReferences { get; set; }

        public Dictionary<string, DependencyDescription> Dependencies { get; set; }

        public List<string> References { get; set; }

        public List<ProjectReference> ProjectReferences { get; set; }

        public List<string> ExportedSourcesFiles { get; set; }
    }
}
