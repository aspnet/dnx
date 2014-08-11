using System;

namespace Microsoft.Framework.Runtime
{
    public interface ICacheContextAccessor
    {
        CacheContext Current { get; set; }
    }
}