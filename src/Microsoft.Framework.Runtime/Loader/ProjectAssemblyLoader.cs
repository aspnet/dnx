// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Common.DependencyInjection;

namespace Microsoft.Framework.Runtime.Loader
{
    internal class ProjectAssemblyLoader : IAssemblyLoader, ILibraryExportProvider
    {
        private readonly IProjectResolver _projectResolver;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<LoaderInformation, Loader> _loaders = new Dictionary<LoaderInformation, Loader>();

        public ProjectAssemblyLoader(IProjectResolver projectResolver, IServiceProvider serviceProvider)
        {
            _projectResolver = projectResolver;
            _serviceProvider = serviceProvider;
        }

        public Assembly Load(string name)
        {
            return ExecuteWith<IAssemblyLoader, Assembly>(name, loader =>
            {
                return loader.Load(name);
            },
            stopIfInitializing: true);
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework, string configuration)
        {
            return ExecuteWith<ILibraryExportProvider, ILibraryExport>(name, exporter =>
            {
                return exporter.GetLibraryExport(name, targetFramework, configuration);
            });
        }

        private TResult ExecuteWith<TInterface, TResult>(string name, Func<TInterface, TResult> execute, bool stopIfInitializing = false) where TResult : class where TInterface : class
        {
            // Don't load anything if there's no project
            Project project;
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            // Get the specific loader
            var loader = _loaders.GetOrAdd(project.Loader, _ => new Loader());

            if (stopIfInitializing && loader.LoaderInitializing)
            {
                return default(TResult);
            }

            if (loader.LoaderInstance == null)
            {
                try
                {
                    loader.LoaderInitializing = true;

                    var assembly = Assembly.Load(new AssemblyName(project.Loader.AssemblyName));

                    var assemblyLoaderType = assembly.GetType(project.Loader.TypeName);

                    loader.LoaderInstance = ActivatorUtilities.CreateInstance(_serviceProvider, assemblyLoaderType);

                    return ExecuteLoaderAsInterface(loader, execute);
                }
                finally
                {
                    loader.LoaderInitializing = false;
                }
            }

            return ExecuteLoaderAsInterface(loader, execute);
        }

        private TResult ExecuteLoaderAsInterface<TInterface, TResult>(Loader loader, Func<TInterface, TResult> executor)
            where TInterface : class
            where TResult : class
        {
            var loaderAsInterface = loader.LoaderInstance as TInterface;

            if (loaderAsInterface != null)
            {
                return executor(loaderAsInterface);
            }

            return null;
        }

        private class Loader
        {
            public object LoaderInstance;
            public bool LoaderInitializing;
        }
    }
}
