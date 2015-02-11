// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.Framework.Runtime.Roslyn
{
    /// <summary>
    /// An implementation of <see cref="ICompilationFailure"/> for Roslyn compilation.
    /// </summary>
    public class RoslynCompilationFailure : ICompilationFailure
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RoslynCompilationFailure"/>.
        /// </summary>
        /// <param name="diagnostics">A sequence of <see cref="Diagnostic"/>s from Roslyn compilation.</param>
        public RoslynCompilationFailure(IEnumerable<Diagnostic> diagnostics)
        {
            var diagnostic = diagnostics.FirstOrDefault();
            if (diagnostic == null)
            {
                throw new ArgumentException("At least one diagnostic must be present.");
            }

            SourceFilePath = diagnostic.Location.GetMappedLineSpan().Path;
            Messages = diagnostics.Select(d => new RoslynCompilationMessage(d));
        }

        /// <inheritdoc />
        public IEnumerable<ICompilationMessage> Messages { get; }

        /// <inheritdoc />
        public string SourceFilePath { get; }

        /// <inheritdoc />
        public string SourceFileContent { get; } = null;

        /// <inheritdoc />
        public string CompiledContent { get; } = null;
    }
}