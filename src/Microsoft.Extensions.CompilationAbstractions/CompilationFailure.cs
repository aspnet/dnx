// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Extensions.CompilationAbstractions
{
    /// <summary>
    /// Describes a failure compiling a specific file. 
    /// </summary>
    public class CompilationFailure
    {
        /// <summary>
        /// Path of the file that produced the compilation failure.
        /// </summary>
        public string SourceFilePath { get; }

        /// <summary>
        /// Contents of the file.
        /// </summary>
        public string SourceFileContent { get; }

        /// <summary>
        /// Contents being compiled.
        /// </summary>
        /// <remarks>
        /// For templated files, the <see cref="SourceFileContent"/> represents the original content and
        /// <see cref="CompiledContent"/> represents the transformed content. This property can be null if
        /// the exception is encountered during transformation.
        /// </remarks>
        public string CompiledContent { get; }

        /// <summary>
        /// Gets a sequence of <see cref="DiagnosticMessage"/> produced as a result of compilation.
        /// </summary>
        public IEnumerable<DiagnosticMessage> Messages { get; }

        public CompilationFailure(string sourceFilePath, IEnumerable<DiagnosticMessage> messages)
            : this(sourceFilePath, sourceFileContent: string.Empty, compiledContent: string.Empty, messages: messages)
        {
        }

        public CompilationFailure(string sourceFilePath, string sourceFileContent, IEnumerable<DiagnosticMessage> messages)
            : this(sourceFilePath, sourceFileContent, compiledContent: string.Empty, messages: messages)
        {
        }

        public CompilationFailure(string sourceFilePath, string sourceFileContent, string compiledContent, IEnumerable<DiagnosticMessage> messages)
        {
            SourceFilePath = sourceFilePath;
            SourceFileContent = sourceFileContent;
            CompiledContent = compiledContent;
            Messages = messages;
        }
    }
}