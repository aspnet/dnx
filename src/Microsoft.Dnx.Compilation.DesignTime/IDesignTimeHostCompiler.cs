using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Compilation.DesignTime
{
    public interface IDesignTimeHostCompiler
    {
        Task<CompileResponse> Compile(string projectPath, CompilationTarget library);
    }
}