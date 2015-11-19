using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.CompilationAbstractions.Caching;

namespace Microsoft.Dnx.Compilation.Caching
{
    public class Cache : ICache
    {
        private readonly ConcurrentDictionary<object, Lazy<CacheEntry>> _entries = new ConcurrentDictionary<object, Lazy<CacheEntry>>();
        private readonly ICacheContextAccessor _accessor;

        public Cache(ICacheContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public object Get(object key, Func<CacheContext, object> factory)
        {
            var entry = _entries.AddOrUpdate(key,
                k => AddEntry(k, factory),
                (k, oldValue) => UpdateEntry(oldValue, k, factory));

            return entry.Value.Result;
        }

        public object Get(object key, Func<CacheContext, object, object> factory)
        {
            var entry = _entries.AddOrUpdate(key,
                k => AddEntry(k, (ctx) => factory(ctx, null)),
                (k, oldValue) => UpdateEntry(oldValue, k, (ctx) => factory(ctx, oldValue.Value.Result)));

            return entry.Value.Result;
        }

        private Lazy<CacheEntry> AddEntry(object k, Func<CacheContext, object> acquire)
        {
            return new Lazy<CacheEntry>(() =>
            {
                var entry = CreateEntry(k, acquire);
                PropagateCacheDependencies(entry);
                return entry;
            });
        }

        private Lazy<CacheEntry> UpdateEntry(Lazy<CacheEntry> currentEntry, object k, Func<CacheContext, object> acquire)
        {
            try
            {
                bool expired = currentEntry.Value.Dependencies.Any(t => t.HasChanged);

                if (expired)
                {
                    // Dispose any entries that are disposable since
                    // we're creating a new one
                    currentEntry.Value.Dispose();

                    return AddEntry(k, acquire);
                }
                else
                {
                    // Logger.TraceInformation("[{0}]: Cache hit for {1}", GetType().Name, k);

                    // Already evaluated
                    PropagateCacheDependencies(currentEntry.Value);
                    return currentEntry;
                }
            }
            catch (Exception)
            {
                return AddEntry(k, acquire);
            }
        }

        private void PropagateCacheDependencies(CacheEntry entry)
        {
            // Bubble up volatile tokens to parent context
            if (_accessor.Current != null)
            {
                foreach (var dependency in entry.Dependencies)
                {
                    _accessor.Current.Monitor(dependency);
                }
            }
        }

        private CacheEntry CreateEntry(object k, Func<CacheContext, object> acquire)
        {
            var entry = new CacheEntry();
            var context = new CacheContext(k, entry.AddCacheDependency);
            CacheContext parentContext = null;
            try
            {
                // Push context
                parentContext = _accessor.Current;
                _accessor.Current = context;

                entry.Result = acquire(context);
            }
            finally
            {
                // Pop context
                _accessor.Current = parentContext;
            }

            // Logger.TraceInformation("[{0}]: Cache miss for {1}", GetType().Name, k);

            entry.CompactCacheDependencies();
            return entry;
        }

        private class CacheEntry : IDisposable
        {
            private IList<ICacheDependency> _dependencies;

            public CacheEntry()
            {
            }

            public IEnumerable<ICacheDependency> Dependencies { get { return _dependencies ?? Enumerable.Empty<ICacheDependency>(); } }

            public object Result { get; set; }

            public void AddCacheDependency(ICacheDependency cacheDependency)
            {
                if (_dependencies == null)
                {
                    _dependencies = new List<ICacheDependency>();
                }

                _dependencies.Add(cacheDependency);
            }

            public void CompactCacheDependencies()
            {
                if (_dependencies != null)
                {
                    _dependencies = _dependencies.Distinct().ToArray();
                }
            }

            public void Dispose()
            {
                (Result as IDisposable)?.Dispose();
            }
        }
    }
}