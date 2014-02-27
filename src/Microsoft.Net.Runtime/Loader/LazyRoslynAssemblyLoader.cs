using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Net.Runtime.FileSystem;
using Microsoft.Net.Runtime.Loader;
using Microsoft.Net.Runtime.Services;
using NuGet;

namespace Microsoft.Net.Runtime
{
    internal class LazyRoslynAssemblyLoader : IAssemblyLoader
    {
        private readonly ProjectResolver _projectResolver;
        private readonly IFileWatcher _watcher;
        private readonly IDependencyExporter _exportResolver;
        private object _roslynLoaderInstance;
        private bool _roslynInitializing;
        private readonly IAssemblyLoaderEngine _loaderEngine;

        public LazyRoslynAssemblyLoader(IAssemblyLoaderEngine loaderEngine,
                                        ProjectResolver projectResolver,
                                        IFileWatcher watcher,
                                        IDependencyExporter exportResolver)
        {
            _loaderEngine = loaderEngine;
            _projectResolver = projectResolver;
            _watcher = watcher;
            _exportResolver = exportResolver;
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            if (_roslynInitializing)
            {
                return null;
            }

            return ExecuteWith<IAssemblyLoader, AssemblyLoadResult>(loader =>
            {
                return loader.Load(loadContext);
            });
        }

        private TResult ExecuteWith<TInterface, TResult>(Func<TInterface, TResult> execute)
        {
            if (_roslynLoaderInstance == null)
            {
                try
                {
                    _roslynInitializing = true;

                    var assembly = Assembly.Load(new AssemblyName("Microsoft.Net.Runtime.Roslyn"));

                    var roslynAssemblyLoaderType = assembly.GetType("Microsoft.Net.Runtime.Roslyn.RoslynAssemblyLoader");

                    var ctors = roslynAssemblyLoaderType.GetTypeInfo().DeclaredConstructors;

                    var args = new object[] { _loaderEngine, _watcher, _projectResolver, _exportResolver };

                    var ctor = ctors.First(c => c.GetParameters().Length == args.Length);

                    _roslynLoaderInstance = ctor.Invoke(args);

                    return execute((TInterface)_roslynLoaderInstance);
                }
                finally
                {
                    _roslynInitializing = false;
                }
            }

            return execute((TInterface)_roslynLoaderInstance);
        }
    }
}
