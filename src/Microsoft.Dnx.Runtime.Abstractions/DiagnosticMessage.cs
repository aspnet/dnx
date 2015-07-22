// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Runtime
{
    /// <summary>
    /// Represents a single diagnostic message, such as a compilation error or a project.json parsing error.
    /// </summary>
    public class DiagnosticMessage
    {
        public DiagnosticMessage(string message, string filePath, DiagnosticMessageSeverity severity)
            : this(message, filePath, severity, startLine: 1, startColumn: 0)
        { }

        public DiagnosticMessage(string message, string filePath, DiagnosticMessageSeverity severity, int startLine, int startColumn)
                : this(
                    message,
                    $"{filePath}({startLine},{startColumn}): {severity.ToString().ToLowerInvariant()}: {message}",
                    filePath,
                    severity,
                    startLine,
                    startColumn,
                    endLine: startLine,
                    endColumn: startColumn)
        { }

        public DiagnosticMessage(
            string message, 
            string formattedMessage, 
            string filePath, 
            DiagnosticMessageSeverity severity, 
            int startLine, 
            int startColumn, 
            int endLine, 
            int endColumn)
        {
            Message = message;
            SourceFilePath = filePath;
            Severity = severity;
            StartLine = startLine;
            EndLine = endLine;
            StartColumn = startColumn;
            EndColumn = endColumn;
            FormattedMessage = formattedMessage;
        }

        /// <summary>
        /// Path of the file that produced the message.
        /// </summary>
        public string SourceFilePath { get; }

        /// <summary>
        /// Gets the error message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the <see cref="DiagnosticMessageSeverity"/>.
        /// </summary>
        public DiagnosticMessageSeverity Severity { get; }

        /// <summary>
        /// Gets the one-based line index for the start of the compilation error.
        /// </summary>
        public int StartLine { get; }

        /// <summary>
        /// Gets the zero-based column index for the start of the compilation error.
        /// </summary>
        public int StartColumn { get; }

        /// <summary>
        /// Gets the one-based line index for the end of the compilation error.
        /// </summary>
        public int EndLine { get; }

        /// <summary>
        /// Gets the zero-based column index for the end of the compilation error.
        /// </summary>
        public int EndColumn { get; }

        /// <summary>
        /// Gets the formatted error message.
        /// </summary>
        public string FormattedMessage { get; }
    }
}