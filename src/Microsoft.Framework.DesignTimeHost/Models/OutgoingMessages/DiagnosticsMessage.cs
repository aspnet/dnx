// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class DiagnosticsMessage
    {
        public DiagnosticsMessage(IList<ICompilationMessage> compilationMessages, FrameworkData frameworkData)
        {
            var errors = compilationMessages
                .Where(msg => msg.Severity == CompilationMessageSeverity.Error)
                .Select(msg => new DiagnosticsInfo
                {
                    Path = msg.SourceFilePath,
                    Line = msg.StartLine,
                    Column = msg.StartColumn,
                    Message = msg.Message,
                    FormattedMessage = msg.FormattedMessage
                });

            var warnings = compilationMessages
                .Where(msg => msg.Severity == CompilationMessageSeverity.Warning)
                .Select(msg => new DiagnosticsInfo
                {
                    Path = msg.SourceFilePath,
                    Line = msg.StartLine,
                    Column = msg.StartColumn,
                    Message = msg.Message,
                    FormattedMessage = msg.FormattedMessage
                });

            Framework = frameworkData;
            CompilationDiagnostics = compilationMessages;
        }

        public DiagnosticsMessage(IEnumerable<DiagnosticsInfo> errors, IEnumerable<DiagnosticsInfo> warnings, FrameworkData framework)
        {
            Errors = errors ?? Enumerable.Empty<DiagnosticsInfo>();
            Warnings = warnings ?? Enumerable.Empty<DiagnosticsInfo>();
            Framework = framework;
        }

        [JsonIgnore]
        public IList<ICompilationMessage> CompilationDiagnostics { get; }

        public FrameworkData Framework { get; }

        public IEnumerable<DiagnosticsInfo> Errors { get; }

        public IEnumerable<DiagnosticsInfo> Warnings { get; }

        public override bool Equals(object obj)
        {
            var other = obj as DiagnosticsMessage;

            return other != null &&
                Enumerable.SequenceEqual(Errors, other.Errors) &&
                Enumerable.SequenceEqual(Warnings, other.Warnings) &&
                Framework == other.Framework;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class DiagnosticsInfo
    {
        public string Path { get; set; }

        public int Line { get; set; }

        public int Column { get; set; }

        public string Message { get; set; }

        public string FormattedMessage { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as DiagnosticsInfo;

            return other != null &&
                 other.Line == Line && 
                 other.Column == Column && 
                 other.Message == Message && 
                 other.FormattedMessage == FormattedMessage;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}