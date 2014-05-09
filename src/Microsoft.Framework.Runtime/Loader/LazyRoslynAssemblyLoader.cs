// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.FileSystem;
using Microsoft.Framework.Runtime.Loader;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    internal class LazyRoslynAssemblyLoader : IAssemblyLoader, ILibraryExportProvider
    {
        private readonly ProjectResolver _projectResolver;
        private readonly IFileWatcher _watcher;
        private readonly ILibraryExportProvider _libraryExporter;
        private object _roslynLoaderInstance;
        private bool _roslynInitializing;
        private readonly IAssemblyLoaderEngine _loaderEngine;

        public LazyRoslynAssemblyLoader(IAssemblyLoaderEngine loaderEngine,
                                        ProjectResolver projectResolver,
                                        IFileWatcher watcher,
                                        ILibraryExportProvider libraryExporter)
        {
            _loaderEngine = loaderEngine;
            _projectResolver = projectResolver;
            _watcher = watcher;
            _libraryExporter = libraryExporter;
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

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework)
        {
            return ExecuteWith<ILibraryExportProvider, ILibraryExport>(exporter =>
            {
                return exporter.GetLibraryExport(name, targetFramework);
            });
        }

        private TResult ExecuteWith<TInterface, TResult>(Func<TInterface, TResult> execute)
        {
            if (_roslynLoaderInstance == null)
            {
                try
                {
                    _roslynInitializing = true;

                    var assembly = Assembly.Load(new AssemblyName("Microsoft.Framework.Runtime.Roslyn"));

                    var roslynAssemblyLoaderType = assembly.GetType("Microsoft.Framework.Runtime.Roslyn.RoslynAssemblyLoader");

                    var ctors = roslynAssemblyLoaderType.GetTypeInfo().DeclaredConstructors;

                    var args = new object[] { 
                        _loaderEngine, 
                        _watcher, 
                        _projectResolver, 
                        _libraryExporter
                    };

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
