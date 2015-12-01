// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.CompilationAbstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages
{
    public class DiagnosticsListMessage
    {
        public DiagnosticsListMessage(IList<DiagnosticMessage> diagnostics) :
            this(diagnostics, frameworkData: null)
        {
        }

        public DiagnosticsListMessage(IList<DiagnosticMessage> diagnostics, FrameworkData frameworkData) :
            this(diagnostics.Select(msg => new DiagnosticMessageView(msg)).ToList(), frameworkData)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }
        }

        public DiagnosticsListMessage(IList<DiagnosticMessageView> diagnostics) :
            this(diagnostics, frameworkData: null)
        {
        }

        public DiagnosticsListMessage(IList<DiagnosticMessageView> diagnostics, FrameworkData frameworkData)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            Diagnostics = diagnostics;
            Errors = diagnostics.Where(msg => msg.Severity == DiagnosticMessageSeverity.Error).ToList();
            Warnings = diagnostics.Where(msg => msg.Severity == DiagnosticMessageSeverity.Warning).ToList();
            Framework = frameworkData;
        }

        public FrameworkData Framework { get; }

        [JsonIgnore]
        public IList<DiagnosticMessageView> Diagnostics { get; }

        public IList<DiagnosticMessageView> Errors { get; }

        public IList<DiagnosticMessageView> Warnings { get; }

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
            var other = obj as DiagnosticsListMessage;

            return other != null &&
                Enumerable.SequenceEqual(Errors, other.Errors) &&
                Enumerable.SequenceEqual(Warnings, other.Warnings) &&
                object.Equals(Framework, other.Framework);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}