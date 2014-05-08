using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public interface IRoslynCompiler
    {
        CompilationContext CompileProject(string name, FrameworkName targetFramework);
    }
}
