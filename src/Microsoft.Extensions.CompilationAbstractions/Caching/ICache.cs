using System;

namespace Microsoft.Extensions.CompilationAbstractions.Caching
{
    public interface ICache
    {
        object Get(object key, Func<CacheContext, object> factory);

        object Get(object key, Func<CacheContext, object, object> factory);
    }
}