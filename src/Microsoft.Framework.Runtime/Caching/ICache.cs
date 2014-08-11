using System;

namespace Microsoft.Framework.Runtime
{

    public interface ICache
    {
        object Get(object key, Func<CacheContext, object> factory);
    }
}