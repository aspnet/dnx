// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public class AfterCompileContext
    {
        public ProjectContext ProjectContext { get; set; }

        public CSharpCompilation Compilation { get; set; }

        public Stream AssemblyStream { get; set; }

        public Stream SymbolStream { get; set; }

        public Stream XmlDocStream { get; set; }

        public IList<Diagnostic> Diagnostics { get; set; }
    }
}
