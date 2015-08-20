using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Testing.xunit;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class FrameworkReferenceResolverFacts
    {
        // NOTE(anurse): Assemblies used for testing:
        // * mscorlib - because obviously
        // * System.Printing - added in 3.0
        // * System.Core - added in 3.5
        // * Microsoft.CSharp - added in 4.0
        // * Microsoft.Build.Engine - provided in 3.0 AND overriden in 3.5

        [ConditionalTheory]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        [SkipIfReferenceAssembliesMissing(@"%WINDIR%\Microsoft.NET\Framework\v2.0.50727")]
        [InlineData("mscorlib", @"%WINDIR%\Microsoft.NET\Framework\v2.0.50727\mscorlib.dll", "2.0.0.0")]
        [InlineData("Microsoft.Build.Engine", @"%WINDIR%\Microsoft.NET\Framework\v2.0.50727\Microsoft.Build.Engine.dll", "2.0.0.0")]
        public void FrameworkResolverResolvesCorrectPaths_Net20(string assemblyName, string expectedPath, string expectedVersion)
        {
            FrameworkResolverResolvesCorrectPaths("net20", assemblyName, expectedPath, expectedVersion);
        }

        [ConditionalTheory]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        [SkipIfReferenceAssembliesMissing(@"%WINDIR%\Microsoft.NET\Framework\v2.0.50727")]
        [SkipIfReferenceAssembliesMissing(@"%REFASMSROOT%\v3.0")]
        [InlineData("mscorlib", @"%WINDIR%\Microsoft.NET\Framework\v2.0.50727\mscorlib.dll", "2.0.0.0")]
        [InlineData("System.Printing", @"%REFASMSROOT%\v3.0\System.Printing.dll", "3.0.0.0")]
        [InlineData("Microsoft.Build.Engine", @"%WINDIR%\Microsoft.NET\Framework\v2.0.50727\Microsoft.Build.Engine.dll", "2.0.0.0")]
        public void FrameworkResolverResolvesCorrectPaths_Net30(string assemblyName, string expectedPath, string expectedVersion)
        {
            FrameworkResolverResolvesCorrectPaths("net30", assemblyName, expectedPath, expectedVersion);
        }

        [ConditionalTheory]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        [SkipIfReferenceAssembliesMissing(@"%WINDIR%\Microsoft.NET\Framework\v2.0.50727")]
        [SkipIfReferenceAssembliesMissing(@"%REFASMSROOT%\v3.0")]
        [SkipIfReferenceAssembliesMissing(@"%REFASMSROOT%\v3.5")]
        [InlineData("mscorlib", @"%WINDIR%\Microsoft.NET\Framework\v2.0.50727\mscorlib.dll", "2.0.0.0")]
        [InlineData("Microsoft.Build.Engine", @"%REFASMSROOT%\v3.5\Microsoft.Build.Engine.dll", "3.5.0.0")]
        [InlineData("System.Core", @"%REFASMSROOT%\v3.5\System.Core.dll", "3.5.0.0")]
        [InlineData("System.Printing", @"%REFASMSROOT%\v3.0\System.Printing.dll", "3.0.0.0")]
        public void FrameworkResolverResolvesCorrectPaths_Net35(string assemblyName, string expectedPath, string expectedVersion)
        {
            FrameworkResolverResolvesCorrectPaths("net35", assemblyName, expectedPath, expectedVersion);
        }

        [ConditionalTheory]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        [SkipIfReferenceAssembliesMissing(@"%REFASMSROOT%\.NETFramework\v4.0")]
        [InlineData("mscorlib", @"%REFASMSROOT%\.NETFramework\v4.0\mscorlib.dll", "4.0.0.0")]
        [InlineData("System.Printing", @"%REFASMSROOT%\.NETFramework\v4.0\System.Printing.dll", "4.0.0.0")]
        [InlineData("System.Core", @"%REFASMSROOT%\.NETFramework\v4.0\System.Core.dll", "4.0.0.0")]
        [InlineData("Microsoft.CSharp", @"%REFASMSROOT%\.NETFramework\v4.0\Microsoft.CSharp.dll", "4.0.0.0")]
        public void FrameworkResolverResolvesCorrectPaths_Net40(string assemblyName, string expectedPath, string expectedVersion)
        {
            FrameworkResolverResolvesCorrectPaths("net40", assemblyName, expectedPath, expectedVersion);
        }

        [ConditionalTheory]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        [SkipIfReferenceAssembliesMissing(@"%REFASMSROOT%\.NETFramework\v4.5")]
        [InlineData("mscorlib", @"%REFASMSROOT%\.NETFramework\v4.5\mscorlib.dll", "4.0.0.0")]
        [InlineData("System.Printing", @"%REFASMSROOT%\.NETFramework\v4.5\System.Printing.dll", "4.0.0.0")]
        [InlineData("System.Core", @"%REFASMSROOT%\.NETFramework\v4.5\System.Core.dll", "4.0.0.0")]
        [InlineData("Microsoft.CSharp", @"%REFASMSROOT%\.NETFramework\v4.5\Microsoft.CSharp.dll", "4.0.0.0")]
        public void FrameworkResolverResolvesCorrectPaths_Net45(string assemblyName, string expectedPath, string expectedVersion)
        {
            FrameworkResolverResolvesCorrectPaths("net45", assemblyName, expectedPath, expectedVersion);
        }

        [ConditionalTheory]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        [SkipIfReferenceAssembliesMissing(@"%REFASMSROOT%\.NETFramework\v4.5.1")]
        [InlineData("mscorlib", @"%REFASMSROOT%\.NETFramework\v4.5.1\mscorlib.dll", "4.0.0.0")]
        [InlineData("System.Printing", @"%REFASMSROOT%\.NETFramework\v4.5.1\System.Printing.dll", "4.0.0.0")]
        [InlineData("System.Core", @"%REFASMSROOT%\.NETFramework\v4.5.1\System.Core.dll", "4.0.0.0")]
        [InlineData("Microsoft.CSharp", @"%REFASMSROOT%\.NETFramework\v4.5.1\Microsoft.CSharp.dll", "4.0.0.0")]
        public void FrameworkResolverResolvesCorrectPaths_Net451(string assemblyName, string expectedPath, string expectedVersion)
        {
            FrameworkResolverResolvesCorrectPaths("net451", assemblyName, expectedPath, expectedVersion);
        }

        [ConditionalTheory]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        [SkipIfReferenceAssembliesMissing(@"%REFASMSROOT%\.NETFramework\v4.5.2")]
        [InlineData("mscorlib", @"%REFASMSROOT%\.NETFramework\v4.5.2\mscorlib.dll", "4.0.0.0")]
        [InlineData("System.Printing", @"%REFASMSROOT%\.NETFramework\v4.5.2\System.Printing.dll", "4.0.0.0")]
        [InlineData("System.Core", @"%REFASMSROOT%\.NETFramework\v4.5.2\System.Core.dll", "4.0.0.0")]
        [InlineData("Microsoft.CSharp", @"%REFASMSROOT%\.NETFramework\v4.5.2\Microsoft.CSharp.dll", "4.0.0.0")]
        public void FrameworkResolverResolvesCorrectPaths_Net452(string assemblyName, string expectedPath, string expectedVersion)
        {
            FrameworkResolverResolvesCorrectPaths("net452", assemblyName, expectedPath, expectedVersion);
        }

        [ConditionalTheory]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        [SkipIfReferenceAssembliesMissing(@"%REFASMSROOT%\.NETFramework\v4.6")]
        [InlineData("mscorlib", @"%REFASMSROOT%\.NETFramework\v4.6\mscorlib.dll", "4.0.0.0")]
        [InlineData("System.Printing", @"%REFASMSROOT%\.NETFramework\v4.6\System.Printing.dll", "4.0.0.0")]
        [InlineData("System.Core", @"%REFASMSROOT%\.NETFramework\v4.6\System.Core.dll", "4.0.0.0")]
        [InlineData("Microsoft.CSharp", @"%REFASMSROOT%\.NETFramework\v4.6\Microsoft.CSharp.dll", "4.0.0.0")]
        public void FrameworkResolverResolvesCorrectPaths_Net46(string assemblyName, string expectedPath, string expectedVersion)
        {
            FrameworkResolverResolvesCorrectPaths("net46", assemblyName, expectedPath, expectedVersion);
        }

        private void FrameworkResolverResolvesCorrectPaths(string shortFrameworkName, string assemblyName, string expectedPath, string expectedVersion)
        {
            var resolver = new FrameworkReferenceResolver();

            string actualPath;
            Version actualVersion;
            Assert.True(resolver.TryGetAssembly(assemblyName, VersionUtility.ParseFrameworkName(shortFrameworkName), out actualPath, out actualVersion));
            Assert.Equal(SkipIfReferenceAssembliesMissingAttribute.ExpandReferenceAssemblyPath(expectedPath), actualPath);

            // Having this be Version->Version equality caused some problems...
            Assert.Equal(Version.Parse(expectedVersion).ToString(), actualVersion.ToString());
        }

        [ConditionalTheory]
        [FrameworkSkipCondition(RuntimeFrameworks.CLR | RuntimeFrameworks.CoreCLR)]
        [InlineData("net20")]
        [InlineData("net30")]
        [InlineData("net35")]
        public void MonoCannotResolveLegacyNetFrameworks(string shortFrameworkName)
        {
            var resolver = new FrameworkReferenceResolver();

            string path;
            Version version;
            Assert.False(resolver.TryGetAssembly("mscorlib", VersionUtility.ParseFrameworkName(shortFrameworkName), out path, out version));
        }

        [ConditionalTheory]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        [InlineData("net20", @"%WINDIR%\Microsoft.NET\Framework\v2.0.50727\{name}.dll")]
        [InlineData("net30", @"%REFASMSROOT%\v3.0\{name}.dll,%WINDIR%\Microsoft.NET\Framework\v2.0.50727\{name}.dll")]
        [InlineData("net35", @"%REFASMSROOT%\v3.5\{name}.dll,%REFASMSROOT%\v3.0\{name}.dll,%WINDIR%\Microsoft.NET\Framework\v2.0.50727\{name}.dll")]
        [InlineData("net40", @"%REFASMSROOT%\.NETFramework\v4.0\{name}.dll,%REFASMSROOT%\.NETFramework\v4.0\Facades\{name}.dll")]
        [InlineData("net45", @"%REFASMSROOT%\.NETFramework\v4.5\{name}.dll,%REFASMSROOT%\.NETFramework\v4.5\Facades\{name}.dll")]
        [InlineData("net451", @"%REFASMSROOT%\.NETFramework\v4.5.1\{name}.dll,%REFASMSROOT%\.NETFramework\v4.5.1\Facades\{name}.dll")]
        [InlineData("net452", @"%REFASMSROOT%\.NETFramework\v4.5.2\{name}.dll,%REFASMSROOT%\.NETFramework\v4.5.2\Facades\{name}.dll")]
        [InlineData("net46", @"%REFASMSROOT%\.NETFramework\v4.6\{name}.dll,%REFASMSROOT%\.NETFramework\v4.6\Facades\{name}.dll")]
        public void FrameworkResolverReturnsCorrectAttemptedPaths(string shortFrameworkName, string attemptedPaths)
        {
            var resolver = new FrameworkReferenceResolver();
            var paths = resolver.GetAttemptedPaths(VersionUtility.ParseFrameworkName(shortFrameworkName));
            Assert.Equal(
                attemptedPaths.Split(',').Select(s => SkipIfReferenceAssembliesMissingAttribute.ExpandReferenceAssemblyPath(s)).ToArray(),
                paths.ToArray());
        }

        [Theory]
        [InlineData("net20", ".NET Framework 2")]
        [InlineData("net30", ".NET Framework 3")]
        [InlineData("net35", ".NET Framework 3.5")]
        [InlineData("net40", ".NET Framework 4")]
        [InlineData("net45", ".NET Framework 4.5")]
        [InlineData("net451", ".NET Framework 4.5.1")]
        [InlineData("net452", ".NET Framework 4.5.2")]
        [InlineData("net46", ".NET Framework 4.6")]
        [InlineData("dnxcore50", "DNX Core 5.0")]
        [InlineData("dnx451", "DNX 4.5.1")]
        [InlineData("dnx452", "DNX 4.5.2")]
        [InlineData("dnx46", "DNX 4.6")]
        [InlineData("dotnet", ".NET Platform")]

        // Legacy
        [InlineData("k10", ".NET Core Framework 4.5")]
        [InlineData("aspnetcore50", "ASP.NET Core 5.0")]
        [InlineData("aspnet50", "ASP.NET 5.0")]
        public void GetFriendlyNameReturnsExpectedNames(string shortName, string friendlyName)
        {
            Assert.Equal(
                friendlyName,
                new FrameworkReferenceResolver().GetFriendlyFrameworkName(VersionUtility.ParseFrameworkName(shortName)));
        }
    }
}
