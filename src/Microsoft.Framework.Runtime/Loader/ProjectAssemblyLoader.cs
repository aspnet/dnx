// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Framework.Runtime.Loader
{
    internal class ProjectAssemblyLoader : IAssemblyLoader
    {
        private readonly IProjectResolver _projectResolver;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<TypeInformation, IAssemblyLoader> _loaders = new Dictionary<TypeInformation, IAssemblyLoader>();

        public ProjectAssemblyLoader(IProjectResolver projectResolver, IServiceProvider serviceProvider)
        {
            _projectResolver = projectResolver;
            _serviceProvider = serviceProvider;
        }

        public Assembly Load(string name)
        {
            // Don't load anything if there's no project
            Project project;
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            var loader = _loaders.GetOrAdd(project.Services.Loader, typeInfo =>
            {
                return ProjectServices.CreateService<IAssemblyLoader>(_serviceProvider, typeInfo);
            });

            // TODO: Handle recursion
            return loader.Load(name);
        }
    }
}
