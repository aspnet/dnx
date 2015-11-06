using Microsoft.Extensions.CompilationAbstractions.Caching;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    internal class FakeCacheContextAccessor : ICacheContextAccessor
    {
        public CacheContext Current { get; set; }
    }
}