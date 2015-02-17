// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public interface IAfterCompileContext
    {
        IProjectContext ProjectContext { get; }

        CSharpCompilation Compilation { get; set; }

        Stream AssemblyStream { get; set; }

        Stream SymbolStream { get; set; }

        Stream XmlDocStream { get; set; }

        IList<Diagnostic> Diagnostics { get; }
    }
}
