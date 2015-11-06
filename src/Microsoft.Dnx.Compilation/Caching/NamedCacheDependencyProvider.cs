using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.CompilationAbstractions.Caching;

namespace Microsoft.Dnx.Compilation.Caching
{
    public class NamedCacheDependencyProvider : INamedCacheDependencyProvider
    {
        private readonly ConcurrentDictionary<string, NamedCacheDependency> _cache = new ConcurrentDictionary<string, NamedCacheDependency>(StringComparer.OrdinalIgnoreCase);

        public static INamedCacheDependencyProvider Empty = new NoopCacheDependencyProvider();

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

        private class NoopCacheDependencyProvider : INamedCacheDependencyProvider
        {
            private static readonly NoopCacheDependency _dependency = new NoopCacheDependency();

            public ICacheDependency GetNamedDependency(string name)
            {
                return _dependency;
            }

            public void Trigger(string name)
            {
            }

            private class NoopCacheDependency : ICacheDependency
            {
                public bool HasChanged
                {
                    get
                    {
                        return false;
                    }
                }
            }
        }
    }
}