// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime
{
    public class FileFormatMessage : ICompilationMessage
    {
        private static readonly string FormattedMessageTemplate = "{0}({1},{2}): {3}: {4}";

        public FileFormatMessage(string message,
                                 string projectFilePath,
                                 CompilationMessageSeverity severity)
        {
            Message = message;
            SourceFilePath = projectFilePath;
            Severity = severity;
        }

        public FileFormatMessage(string message,
                                 string projectFilePath,
                                 CompilationMessageSeverity severity,
                                 JToken token)
        {
            Message = message;
            SourceFilePath = projectFilePath;
            Severity = severity;

            var lineInfo = (IJsonLineInfo)token;

            StartColumn = lineInfo.LinePosition;
            EndColumn = StartColumn;
            StartLine = lineInfo.LineNumber;
            EndLine = StartLine;
        }

        public string FormattedMessage
        {
            get
            {
                return string.Format(FormattedMessageTemplate, SourceFilePath, StartLine, StartColumn, Severity.ToString().ToLower(), Message);
            }
        }

        public string Message { get; }

        public string SourceFilePath { get; }

        public CompilationMessageSeverity Severity { get; }

        public int StartLine { get; set; }

        public int StartColumn { get; set; }

        public int EndLine { get; set; }

        public int EndColumn { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as FileFormatMessage;

            return other != null &&
                StartLine == other.StartLine &&
                StartColumn == other.StartColumn &&
                Message.Equals(other.Message, StringComparison.Ordinal) &&
                SourceFilePath.Equals(other.SourceFilePath, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
