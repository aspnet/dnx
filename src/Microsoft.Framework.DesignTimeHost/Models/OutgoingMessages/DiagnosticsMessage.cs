// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Collections.Generic;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class DiagnosticsMessage
    {
        public DiagnosticsMessage(IEnumerable<ICompilationMessage> compilationMessages, FrameworkData frameworkData)
        {
            CompilationDiagnostics = compilationMessages.ToList();
            Errors = compilationMessages.Where(msg => msg.Severity == CompilationMessageSeverity.Error).ToList();
            Warnings = compilationMessages.Where(msg => msg.Severity == CompilationMessageSeverity.Warning).ToList();
            Framework = frameworkData;
        }

        public FrameworkData Framework { get; }

        [JsonIgnore]
        public IList<ICompilationMessage> CompilationDiagnostics { get; }

        public IList<ICompilationMessage> Errors { get; }

        public IList<ICompilationMessage> Warnings { get; }

        public IList<string> FormattedErrors => Errors.Select(e => e.FormattedMessage).ToList();

        public IList<string> FormattedWarnings => Warnings.Select(w => w.FormattedMessage).ToList();

        public static JToken ConvertToJson(int protocolVersion, IList<DiagnosticsMessage> messages)
        {
            if (protocolVersion <= 1)
            {
                var list = messages.Select(message => new
                {
                    Framework = message.Framework,
                    Errors = message.Errors.Select(e => e.FormattedMessage).ToList(),
                    Warnings = message.Warnings.Select(w => w.FormattedMessage).ToList()
                }).ToList();

                return JToken.FromObject(list);
            }
            else
            {
                return JToken.FromObject(messages);
            }
        }

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
}