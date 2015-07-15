// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Framework.Runtime.Roslyn
{
    /// <summary>
    /// An implementation of <see cref="ICompilationMessage"/> that wraps 
    /// <see cref="Diagnostic"/> instances from Roslyn compilation.
    /// </summary>
    public class RoslynCompilationMessage : ICompilationMessage
    {
        private readonly Diagnostic _diagnostic;
        private readonly FileLinePositionSpan _mappedLineSpan;
        private readonly FrameworkName _targetFramework;

        /// <summary>
        /// Initializes a new instance of <see cref="RoslynCompilationMessage"/>.
        /// </summary>
        /// <param name="diagnostic">The <see cref="Diagnostic"/> instance to read
        /// diagnostic details from.</param>
        public RoslynCompilationMessage(Diagnostic diagnostic)
            : this(diagnostic, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RoslynCompilationMessage"/>.
        /// </summary>
        /// <param name="diagnostic">The <see cref="Diagnostic"/> instance to read
        /// diagnostic details from.</param>
        /// <param name="targetFramework">The <see cref="FrameworkName"> in which this 
        /// diagnostic is generated.</param>
        public RoslynCompilationMessage(Diagnostic diagnostic, FrameworkName targetFramework)
        {
            _targetFramework = targetFramework;
            _diagnostic = diagnostic;
            _mappedLineSpan = _diagnostic.Location.GetMappedLineSpan();

            switch (_diagnostic.Severity)
            {
                case DiagnosticSeverity.Error:
                    Severity = CompilationMessageSeverity.Error;
                    break;
                case DiagnosticSeverity.Warning:
                    Severity = CompilationMessageSeverity.Warning;
                    break;
                default:
                    Severity = CompilationMessageSeverity.Info;
                    break;
            }
        }

        /// <inheritdoc />
        public string SourceFilePath => _mappedLineSpan.Path;

        /// <inheritdoc />
        public CompilationMessageSeverity Severity { get; }

        /// <inheritdoc />
        public int EndColumn => _mappedLineSpan.EndLinePosition.Character + 1;

        /// <inheritdoc />
        public int EndLine => _mappedLineSpan.EndLinePosition.Line + 1;

        /// <inheritdoc />
        public string Message => _diagnostic.GetMessage();

        /// <inheritdoc />
        public string FormattedMessage => RoslynDiagnosticFormatter.Format(_diagnostic, _targetFramework);

        /// <inheritdoc />
        public int StartColumn => _mappedLineSpan.StartLinePosition.Character + 1;

        /// <inheritdoc />
        public int StartLine => _mappedLineSpan.StartLinePosition.Line + 1;

        /// <inheritdoc />
        public override string ToString()
        {
            return FormattedMessage;
        }
    }
}