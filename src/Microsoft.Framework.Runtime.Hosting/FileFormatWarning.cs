// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime
{
    public class FileFormatWarning : ICompilationMessage
    {
        private static readonly string FormattedMessageTemplate = "{0}({1},{2}): warning: {3}";

        public FileFormatWarning(string message, string projectFilePath, JToken token)
        {
            Message = message;
            SourceFilePath = projectFilePath;

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
                return string.Format(FormattedMessageTemplate, SourceFilePath, StartLine, StartColumn, Message);
            }
        }

        public string Message { get; }

        public string SourceFilePath { get; }

        public CompilationMessageSeverity Severity { get { return CompilationMessageSeverity.Warning; } }

        public int StartLine { get; }

        public int StartColumn { get; }

        public int EndLine { get; }

        public int EndColumn { get; }

        public override bool Equals(object obj)
        {
            var other = obj as FileFormatWarning;

            return other != null &&
                StartLine == other.StartLine &&
                StartColumn == other.StartColumn &&
                Message.Equals(other.Message, System.StringComparison.Ordinal) &&
                SourceFilePath.Equals(other.SourceFilePath, System.StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
