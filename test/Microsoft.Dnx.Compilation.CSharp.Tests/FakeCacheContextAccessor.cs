using Microsoft.Extensions.Compilation.Caching;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    internal class FakeCacheContextAccessor : ICacheContextAccessor
    {
        public CacheContext Current { get; set; }
    }
}