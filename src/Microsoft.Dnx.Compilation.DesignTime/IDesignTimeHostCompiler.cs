using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.Compilation;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Compilation.DesignTime
{
    public interface IDesignTimeHostCompiler
    {
        Task<CompileResponse> Compile(string projectPath, CompilationTarget library);
    }
}