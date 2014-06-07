// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Common.DependencyInjection;

namespace Microsoft.Framework.Runtime
{
    internal class ProjectAssemblyLoader : IAssemblyLoader, ILibraryExportProvider
    {
        private readonly IProjectResolver _projectResolver;
        private readonly IServiceProvider _serviceProvider;
        private object _loaderInstance;
        private bool _loaderInitializing;

        public ProjectAssemblyLoader(IProjectResolver projectResolver, IServiceProvider serviceProvider)
        {
            _projectResolver = projectResolver;
            _serviceProvider = serviceProvider;
        }

        public Assembly Load(string name)
        {
            if (_loaderInitializing)
            {
                return null;
            }

            return ExecuteWith<IAssemblyLoader, Assembly>(name, loader =>
            {
                return loader.Load(name);
            });
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework)
        {
            return ExecuteWith<ILibraryExportProvider, ILibraryExport>(name, exporter =>
            {
                return exporter.GetLibraryExport(name, targetFramework);
            });
        }

        private TResult ExecuteWith<TInterface, TResult>(string name, Func<TInterface, TResult> execute) where TResult : class where TInterface : class
        {
            // Don't load anything if there's no project
            Project project;
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            if (_loaderInstance == null)
            {
                try
                {
                    _loaderInitializing = true;

                    var assembly = Assembly.Load(new AssemblyName("Microsoft.Framework.Runtime.Roslyn"));

                    var assemblyLoaderType = assembly.GetType("Microsoft.Framework.Runtime.Roslyn.RoslynAssemblyLoader");

                    _loaderInstance = ActivatorUtilities.CreateInstance(_serviceProvider, assemblyLoaderType);

                    return ExecuteLoaderAsInterface(execute);
                }
                finally
                {
                    _loaderInitializing = false;
                }
            }

            return ExecuteLoaderAsInterface(execute);
        }

        private TResult ExecuteLoaderAsInterface<TInterface, TResult>(Func<TInterface, TResult> executor)
            where TInterface : class
            where TResult : class
        {
            var loaderAsInterface = _loaderInstance as TInterface;

            if (loaderAsInterface != null)
            {
                return executor(loaderAsInterface);
            }

            return null;
        }
    }
}
