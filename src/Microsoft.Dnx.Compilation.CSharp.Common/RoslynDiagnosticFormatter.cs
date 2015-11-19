// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public class RoslynDiagnosticFormatter
    {
        public static string Format(Diagnostic diagnostic, FrameworkName targetFramework, IFormatProvider formatter = null)
        {
            if (diagnostic == null)
            {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            var culture = formatter as CultureInfo;

            switch (diagnostic.Location.Kind)
            {
                case LocationKind.SourceFile:
                case LocationKind.XmlFile:
                case LocationKind.ExternalFile:
                    var span = diagnostic.Location.GetLineSpan();
                    var mappedSpan = diagnostic.Location.GetMappedLineSpan();
                    if (!span.IsValid || !mappedSpan.IsValid)
                    {
                        goto default;
                    }

                    string path, basePath;
                    if (mappedSpan.HasMappedPath)
                    {
                        path = mappedSpan.Path;
                        basePath = span.Path;
                    }
                    else
                    {
                        path = span.Path;
                        basePath = null;
                    }

                    var start = mappedSpan.Span.Start;
                    var framework = targetFramework == null ? string.Empty : $" {targetFramework.FullName}";
                    return $"{path}({start.Line + 1},{start.Character + 1}):{framework} {GetMessagePrefix(diagnostic)}: {diagnostic.GetMessage(culture)}";
                default:
                    return $"{GetMessagePrefix(diagnostic)}: {diagnostic.GetMessage(culture)}";
            }
        }

        private static string GetMessagePrefix(Diagnostic diagnostic)
        {
            string prefix;
            switch (diagnostic.Severity)
            {
                case DiagnosticSeverity.Hidden:
                    prefix = "hidden";
                    break;
                case DiagnosticSeverity.Info:
                    prefix = "info";
                    break;
                case DiagnosticSeverity.Warning:
                    prefix = "warning";
                    break;
                case DiagnosticSeverity.Error:
                    prefix = "error";
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected value '{diagnostic}' of type '{diagnostic?.GetType()?.FullName ?? "<unknown>"}'");
            }

            return $"{prefix} {diagnostic.Id}";
        }
    }
}
