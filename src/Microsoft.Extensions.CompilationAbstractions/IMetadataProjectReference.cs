// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Extensions.CompilationAbstractions
{
    public interface IMetadataProjectReference : IMetadataReference
    {
        string ProjectPath { get; }

        DiagnosticResult GetDiagnostics();

        IList<ISourceReference> GetSources();

        Assembly Load(AssemblyName assemblyName, IAssemblyLoadContext loadContext);

        void EmitReferenceAssembly(Stream stream);

        DiagnosticResult EmitAssembly(string outputPath);
    }
}
