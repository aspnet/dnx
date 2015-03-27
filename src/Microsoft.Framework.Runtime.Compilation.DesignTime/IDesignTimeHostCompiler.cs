using System.Threading.Tasks;

namespace Microsoft.Framework.Runtime
{
    public interface IDesignTimeHostCompiler
    {
        Task<CompileResponse> Compile(string projectPath, ILibraryKey library);
    }
}