using Microsoft.CodeAnalysis;

namespace Microsoft.Dnx.Compilation.CSharp
{
    internal class RoslynDiagnostics
    {
        internal static readonly DiagnosticDescriptor StrongNamingNotSupported = new DiagnosticDescriptor(
            id: "DNX1001",
            title: "Strong name generation is not supported on this platform",
            messageFormat: "Strong name generation is not supported on CoreCLR. Skipping strong name generation.",
            category: "StrongNaming",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor SnkNotSupportedOnMono = new DiagnosticDescriptor(
            id: "DNX1002",
            title: "Signing assemblies using a key file is not supported on Mono",
            messageFormat: "Signing assemblies using a key file is not supported on Mono. Using OSS signing instead.",
            category: "StrongNaming",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor OssAndSnkSigningAreExclusive = new DiagnosticDescriptor(
            id: "DNX1003",
            title: "The \"keyFile\" and \"strongName\" options are mutually exclusive",
            messageFormat: "The \"keyFile\" and \"strongName\" options are mutually exclusive and cannot be used together.",
            category: "StrongNaming",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}