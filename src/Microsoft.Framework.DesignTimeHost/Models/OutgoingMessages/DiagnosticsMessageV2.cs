// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Collections.Generic;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class DiagnosticsMessageV2
    {
        public DiagnosticsMessageV2(IEnumerable<ICompilationMessage> compilationMessages, FrameworkData frameworkData)
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

        public override bool Equals(object obj)
        {
            var other = obj as DiagnosticsMessageV2;

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