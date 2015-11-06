using System;

namespace Microsoft.Extensions.CompilationAbstractions.Caching
{
    public class CacheContext
    {
        public object Key { get; private set; }

        public Action<ICacheDependency> Monitor { get; private set; }

        public CacheContext(object key, Action<ICacheDependency> monitor)
        {
            Key = key;
            Monitor = monitor;
        }
    }
}