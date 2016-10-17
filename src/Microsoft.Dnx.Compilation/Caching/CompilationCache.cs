using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.CompilationAbstractions.Caching;

namespace Microsoft.Dnx.Compilation.Caching
{
    public class CompilationCache
    {
        public ICache Cache { get; }
        public ICacheContextAccessor CacheContextAccessor { get; }
        public INamedCacheDependencyProvider NamedCacheDependencyProvider { get; }

        public CompilationCache()
        {
            CacheContextAccessor = new CacheContextAccessor();
            Cache = new Cache(CacheContextAccessor);
            NamedCacheDependencyProvider = new NamedCacheDependencyProvider();
        }
    }
}
