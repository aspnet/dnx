// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Framework.Runtime.Compilation;

namespace Microsoft.Framework.Runtime.Compilation.DesignTime
{
    public class CompileResponse
    {
        public IList<DiagnosticMessage> Diagnostics { get; set; }

        public IDictionary<string, byte[]> EmbeddedReferences { get; set; }

        public byte[] AssemblyBytes { get; set; }
        public byte[] PdbBytes { get; set; }

        public string AssemblyPath { get; set; }
    }
}