using System;
using System.Collections.Concurrent;

namespace Microsoft.Framework.Runtime
{
    public class NamedCacheDependencyProvider : INamedCacheDependencyProvider
    {
        private readonly ConcurrentDictionary<string, NamedCacheDependency> _cache = new ConcurrentDictionary<string, NamedCacheDependency>(StringComparer.OrdinalIgnoreCase);

        public ICacheDependency GetNamedDependency(string name)
        {
            return _cache.GetOrAdd(name, key =>
            {
                return new NamedCacheDependency(key);
            });
        }

        public void Trigger(string name)
        {
            NamedCacheDependency dependency;

            if (_cache.TryRemove(name, out dependency))
            {
                dependency.SetChanged();
            }
        }
    }
}