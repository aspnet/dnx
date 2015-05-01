// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.Internal;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class DiagnosticsMessage
    {
        public DiagnosticsMessage([NotNull] IList<ICompilationMessage> diagnostics) :
            this(diagnostics, frameworkData: null)
        {
        }

        public DiagnosticsMessage([NotNull] IList<ICompilationMessage> diagnostics, FrameworkData frameworkData)
        {
            Diagnostics = diagnostics;
            Errors = diagnostics.Where(msg => msg.Severity == CompilationMessageSeverity.Error).ToList();
            Warnings = diagnostics.Where(msg => msg.Severity == CompilationMessageSeverity.Warning).ToList();
            Framework = frameworkData;
        }

        public FrameworkData Framework { get; }

        [JsonIgnore]
        public IList<ICompilationMessage> Diagnostics { get; }

        public IList<ICompilationMessage> Errors { get; }

        public IList<ICompilationMessage> Warnings { get; }

        public virtual JToken ConvertToJson(int protocolVersion)
        {
            if (protocolVersion <= 1)
            {
                var payload = new
                {
                    Framework = Framework ?? new FrameworkData { },
                    Errors = Errors.Select(e => e.FormattedMessage),
                    Warnings = Warnings.Select(w => w.FormattedMessage)
                };

                return JToken.FromObject(payload);
            }
            else
            {
                return JToken.FromObject(this);
            }
        }

        public override bool Equals(object obj)
        {
            var other = obj as DiagnosticsMessage;

            return other != null &&
                Enumerable.SequenceEqual(Errors, other.Errors, Comparer.Default) &&
                Enumerable.SequenceEqual(Warnings, other.Warnings, Comparer.Default) &&
                object.Equals(Framework, other.Framework);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        private class Comparer : IEqualityComparer<ICompilationMessage>
        {
            public static readonly Comparer Default = new Comparer();

            public bool Equals(ICompilationMessage x, ICompilationMessage y)
            {
                return x.StartLine == y.StartLine &&
                       x.StartColumn == y.StartColumn &&
                       string.Equals(x.Message, y.Message, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(x.SourceFilePath, y.SourceFilePath, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(ICompilationMessage obj)
            {
                var hash = obj.StartLine.GetHashCode() ^ obj.StartColumn.GetHashCode() ^ obj.Message.GetHashCode();

                if (obj.SourceFilePath != null)
                {
                    hash ^= obj.SourceFilePath.GetHashCode();
                }

                return hash;
            }
        }
    }
}