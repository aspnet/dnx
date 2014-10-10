using System;
using System.Collections.Concurrent;

namespace Microsoft.Framework.Runtime
{
    public interface INamedCacheDependencyProvider
    {
        ICacheDependency GetNamedDependency(string name);
        void Trigger(string name);
    }
}