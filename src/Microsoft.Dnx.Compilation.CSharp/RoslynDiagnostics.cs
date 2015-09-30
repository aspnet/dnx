using Microsoft.CodeAnalysis;

namespace Microsoft.Dnx.Compilation.CSharp
{
    internal class RoslynDiagnostics
    {
        internal static readonly DiagnosticDescriptor RealSigningSupportedOnlyOnDesktopClr = new DiagnosticDescriptor(
            id: "DNX1001",
            title: "Strong name generation with a private and public key pair is not supported on this platform",
            messageFormat: "Strong name generation with a private and public key pair is supported only on desktop CLR. Falling back to OSS signing.",
            category: "StrongNaming",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}