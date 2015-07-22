using System.Threading.Tasks;

namespace Microsoft.Framework.Runtime.Compilation.DesignTime
{
    public interface IDesignTimeHostCompiler
    {
        Task<CompileResponse> Compile(string projectPath, CompilationTarget library);
    }
}