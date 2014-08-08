// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    public class FileWriteTimeChangedToken : IToken
    {
        private readonly string _path;
        private readonly DateTime _lastWriteTime;

        public FileWriteTimeChangedToken(string path)
        {
            _path = path;
            _lastWriteTime = File.GetLastWriteTime(path);
        }

        public bool HasChanged
        {
            get
            {
                return _lastWriteTime < File.GetLastWriteTime(_path);
            }
        }

        public override string ToString()
        {
            return _path;
        }

        public override bool Equals(object obj)
        {
            var token = obj as FileWriteTimeChangedToken;
            return token != null && token._path.Equals(_path, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return _path.GetHashCode();
        }
    }

    public interface IToken
    {
        bool HasChanged { get; }
    }

    public interface ICache
    {
        object Get(object key, Func<CacheContext, object> factory);
    }

    public class CacheContext
    {
        public object Key { get; private set; }

        public Action<IToken> Monitor { get; private set; }

        public CacheContext(object key, Action<IToken> monitor)
        {
            Key = key;
            Monitor = monitor;
        }
    }

    public interface ICacheContextAccessor
    {
        CacheContext Current { get; set; }
    }

    public class CacheContextAccessor : ICacheContextAccessor
    {
        [ThreadStatic]
        private static CacheContext _threadInstance;

        public static CacheContext ThreadInstance
        {
            get { return _threadInstance; }
            set { _threadInstance = value; }
        }

        public CacheContext Current
        {
            get { return ThreadInstance; }
            set { ThreadInstance = value; }
        }
    }

    public static class CacheExtensions
    {
        public static T Get<T>(this ICache cache, object key, Func<CacheContext, T> factory)
        {
            return (T)cache.Get(key, ctx => factory(ctx));
        }
    }

    public class Cache : ICache
    {
        private readonly ConcurrentDictionary<object, Lazy<CacheEntry>> _entries = new ConcurrentDictionary<object, Lazy<CacheEntry>>();
        private readonly ICacheContextAccessor _accessor;

        public Cache() : this(new CacheContextAccessor())
        {
        }

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

        private Lazy<CacheEntry> AddEntry(object k, Func<CacheContext, object> acquire)
        {
            return new Lazy<CacheEntry>(() =>
            {
                var entry = CreateEntry(k, acquire);
                PropagateTokens(entry);
                return entry;
            });
        }

        private Lazy<CacheEntry> UpdateEntry(Lazy<CacheEntry> currentEntry, object k, Func<CacheContext, object> acquire)
        {
            bool expired = currentEntry.Value.Tokens.Any(t => t.HasChanged);

            if (expired)
            {
                return AddEntry(k, acquire);
            }
            else
            {
                Trace.TraceInformation("[{0}]: Cache hit for {1}", GetType().Name, k);

                // Already evaluated
                PropagateTokens(currentEntry.Value);
                return currentEntry;
            }
        }

        private void PropagateTokens(CacheEntry entry)
        {
            // Bubble up volatile tokens to parent context
            if (_accessor.Current != null)
            {
                foreach (var token in entry.Tokens)
                {
                    _accessor.Current.Monitor(token);
                }
            }
        }

        private CacheEntry CreateEntry(object k, Func<CacheContext, object> acquire)
        {
            var entry = new CacheEntry();
            var context = new CacheContext(k, entry.AddToken);
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

            Trace.TraceInformation("[{0}]: Cache miss for {1}", GetType().Name, k);

            entry.CompactTokens();
            return entry;
        }

        private class CacheEntry
        {
            private IList<IToken> _tokens;

            public CacheEntry()
            {

            }

            public IEnumerable<IToken> Tokens { get { return _tokens ?? Enumerable.Empty<IToken>(); } }

            public object Result { get; set; }

            public void AddToken(IToken token)
            {
                if (_tokens == null)
                {
                    _tokens = new List<IToken>();
                }

                _tokens.Add(token);
            }

            public void CompactTokens()
            {
                if (_tokens != null)
                {
                    _tokens = _tokens.Distinct().ToArray();
                }
            }
        }
    }

    public class ProjectLibraryExportProvider : ILibraryExportProvider
    {
        private readonly IProjectResolver _projectResolver;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<TypeInformation, IProjectReferenceProvider> _projectReferenceProviders = new Dictionary<TypeInformation, IProjectReferenceProvider>();

        public ProjectLibraryExportProvider(IProjectResolver projectResolver,
                                            IServiceProvider serviceProvider)
        {
            _projectResolver = projectResolver;
            _serviceProvider = serviceProvider;
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework, string configuration)
        {
            Project project;
            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            Trace.TraceInformation("[{0}]: GetLibraryExport({1}, {2}, {3})", GetType().Name, name, targetFramework, configuration);

            var targetFrameworkInformation = project.GetTargetFramework(targetFramework);

            // This is the target framework defined in the project. If there were no target frameworks
            // defined then this is the targetFramework specified
            targetFramework = targetFrameworkInformation.FrameworkName ?? targetFramework;

            var key = Tuple.Create(name, targetFramework, configuration);

            var cache = (ICache)_serviceProvider.GetService(typeof(ICache));

            return cache.Get<ILibraryExport>(key, ctx =>
            {
                // Get the composite library export provider
                var exportProvider = (ILibraryExportProvider)_serviceProvider.GetService(typeof(ILibraryExportProvider));
                var libraryManager = (ILibraryManager)_serviceProvider.GetService(typeof(ILibraryManager));

                // Get the exports for the project dependencies
                ILibraryExport projectExport = ProjectExportProviderHelper.GetExportsRecursive(
                   libraryManager,
                   exportProvider,
                   project.Name,
                   targetFramework,
                   configuration,
                   dependenciesOnly: true);

                var metadataReferences = new List<IMetadataReference>();
                var sourceReferences = new List<ISourceReference>();

                if (!string.IsNullOrEmpty(targetFrameworkInformation.AssemblyPath))
                {
                    var assemblyPath = ResolvePath(project, configuration, targetFrameworkInformation.AssemblyPath);
                    var pdbPath = ResolvePath(project, configuration, targetFrameworkInformation.PdbPath);

                    metadataReferences.Add(new CompiledProjectMetadataReference(project, assemblyPath, pdbPath));
                }
                else
                {
                    // Find the default project exporter
                    var projectReferenceProvider = _projectReferenceProviders.GetOrAdd(project.LanguageServices.ProjectReferenceProvider, typeInfo =>
                   {
                       return LanguageServices.CreateService<IProjectReferenceProvider>(_serviceProvider, typeInfo);
                   });

                    Trace.TraceInformation("[{0}]: GetProjectReference({1}, {2}, {3})", project.LanguageServices.ProjectReferenceProvider.TypeName, name, targetFramework, configuration);

                    // Resolve the project export
                    IMetadataProjectReference projectReference = projectReferenceProvider.GetProjectReference(
                       project,
                       targetFramework,
                       configuration,
                       projectExport.MetadataReferences,
                       projectExport.SourceReferences,
                       metadataReferences);

                    metadataReferences.Add(projectReference);

                    // Shared sources
                    foreach (var sharedFile in project.SharedFiles)
                    {
                        sourceReferences.Add(new SourceFileReference(sharedFile));
                    }
                }

                return new LibraryExport(metadataReferences, sourceReferences);
            });
        }

        private static string ResolvePath(Project project, string configuration, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (Path.DirectorySeparatorChar == '/')
            {
                path = path.Replace('\\', Path.DirectorySeparatorChar);
            }
            else
            {
                path = path.Replace('/', Path.DirectorySeparatorChar);
            }

            path = path.Replace("{configuration}", configuration);

            return Path.Combine(project.ProjectDirectory, path);
        }
    }
}
