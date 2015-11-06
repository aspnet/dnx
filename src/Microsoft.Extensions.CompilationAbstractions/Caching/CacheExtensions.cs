using System;

namespace Microsoft.Extensions.CompilationAbstractions.Caching
{
    public static class CacheExtensions
    {
        public static T Get<T>(this ICache cache, object key, Func<CacheContext, T> factory)
        {
            return (T)cache.Get(key, ctx => factory(ctx));
        }

        public static T Get<T>(this ICache cache, object key, Func<CacheContext, T, T> factory)
        {
            return (T)cache.Get(key, (ctx, oldValue) => factory(ctx, (T)oldValue));
        }
    }
}