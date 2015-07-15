// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;

namespace Microsoft.Framework.Runtime.Roslyn
{
    /// <summary>
    /// An implementation of <see cref="ICompilationException"/> representing Roslyn compilation exceptions.
    /// </summary>
    public class RoslynCompilationException : Exception, ICompilationException
    {
        private IEnumerable<ICompilationFailure> _compilationFailure;

        /// <summary>
        /// Initializes a new instance of <see cref="RoslynCompilationException"/>.
        /// </summary>
        /// <param name="assemblyName">The assembly that produced the compilation exception.</param>
        /// <param name="diagnostics">Diagnostics from Roslyn compilation.</param>
        /// <param name="targetFramework">Target framework the compilation exection is thrown from.</param>
        public RoslynCompilationException(IEnumerable<Diagnostic> diagnostics, FrameworkName targetFramework)
            : base(GetErrorMessage(diagnostics, targetFramework))
        {
            Diagnostics = diagnostics;
        }

        /// <summary>
        /// Gets the <see cref="IEnumerable{T}"/> of <see cref="Diagnostic"/> from Roslyn compilation.
        /// </summary>
        public IEnumerable<Diagnostic> Diagnostics { get; }

        /// <inheritdoc />
        public IEnumerable<ICompilationFailure> CompilationFailures
        {
            get
            {
                if (_compilationFailure == null)
                {
                    _compilationFailure = Diagnostics.GroupBy(d => d.Location.GetMappedLineSpan().Path, StringComparer.OrdinalIgnoreCase)
                                                     .Select(d => new RoslynCompilationFailure(d));
                }

                return _compilationFailure;
            }
        }

        private static string GetErrorMessage(IEnumerable<Diagnostic> diagnostics, FrameworkName targetFramework)
        {
            return string.Join(Environment.NewLine,
                               diagnostics.Select(diagnostic => RoslynDiagnosticFormatter.Instance.Format(diagnostic, targetFramework)));
        }
    }
}