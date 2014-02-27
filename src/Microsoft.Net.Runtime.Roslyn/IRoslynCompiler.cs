using System.Runtime.Versioning;

namespace Microsoft.Net.Runtime.Roslyn
{
    public interface IRoslynCompiler
    {
        CompilationContext CompileProject(string name, FrameworkName targetFramework);
    }
}
