using System.IO;
using Microsoft.Dnx.Runtime;
using Xunit;

namespace Microsoft.Dnx.Compilation.CSharp.Common.Tests
{
    public class CompilerOptionsExtensionsFacts
    {
        [Fact(Skip = "Can only enable after changes are checked in.")]
        public void ResolvesFullPathToKeyFile()
        {
            var compilerOptions = new CompilerOptions { KeyFile = "../../tools/Key.snk" };
            var projectDirectory = Path.GetFullPath("/solution/src/project/");
            var compilationSettings = compilerOptions.ToCompilationSettings(
                new System.Runtime.Versioning.FrameworkName("A Framework, Version=v1.0"),
                projectDirectory);

            Assert.Equal(Path.GetFullPath("/solution/tools/Key.snk"), compilationSettings.CompilationOptions.CryptoKeyFile);
        }
    }
}
