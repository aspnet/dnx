// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.DependencyInjection;

namespace Microsoft.Framework.PackageManager
{
    public class ProjectBuilder : IProjectBuilder, ILibraryExportProvider
    {
        private readonly IProjectResolver _projectResolver;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<TypeInformation, object> _builders = new Dictionary<TypeInformation, object>();

        public ProjectBuilder(IProjectResolver projectResolver, IServiceProvider serviceProvider)
        {
            _projectResolver = projectResolver;
            _serviceProvider = serviceProvider;
        }

        public IProjectBuildResult Build(string name, FrameworkName targetFramework, string configuration, string outputPath)
        {
            return ExecuteWith<IProjectBuilder, IProjectBuildResult>(name, builder =>
            {
                return builder.Build(name, targetFramework, configuration, outputPath);
            });
        }


        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework, string configuration)
        {
            return ExecuteWith<ILibraryExportProvider, ILibraryExport>(name, exporter =>
            {
                return exporter.GetLibraryExport(name, targetFramework, configuration);
            });
        }

        private TResult ExecuteWith<TInterface, TResult>(string name, Func<TInterface, TResult> execute)
            where TResult : class
            where TInterface : class
        {
            // Don't load anything if there's no project
            Project project;
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            // Get the specific loader
            var builder = _builders.GetOrAdd(project.Builder, info =>
            {
                var assembly = Assembly.Load(new AssemblyName(info.AssemblyName));

                var assemblyLoaderType = assembly.GetType(info.TypeName);

                return ActivatorUtilities.CreateInstance(_serviceProvider, assemblyLoaderType);
            });

            return ExecuteAsInterface(builder, execute);
        }

        private TResult ExecuteAsInterface<TInterface, TResult>(object builder, Func<TInterface, TResult> executor)
            where TInterface : class
            where TResult : class
        {
            var builderAsInterface = builder as TInterface;

            if (builderAsInterface != null)
            {
                return executor(builderAsInterface);
            }

            return default(TResult);
        }
    }
}