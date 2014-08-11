using System;

namespace Microsoft.Framework.Runtime
{
    public static class CacheExtensions
    {
        public static T Get<T>(this ICache cache, object key, Func<CacheContext, T> factory)
        {
            return (T)cache.Get(key, ctx => factory(ctx));
        }
    }
}