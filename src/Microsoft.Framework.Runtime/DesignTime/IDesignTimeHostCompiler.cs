using System;
using System.Threading.Tasks;

namespace Microsoft.Framework.Runtime
{
    public interface IDesignTimeHostCompiler
    {
        Task<CompileResponse> Compile(CompileRequest request);
    }
}