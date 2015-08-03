// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime.Compilation;

namespace Microsoft.Dnx.Runtime.Loader
{
    internal class ProjectAssemblyLoader : IAssemblyLoader
    {
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;
        private readonly ICompilationEngine _compilationEngine;
        private readonly IProjectResolver _projectResolver;
        private readonly FrameworkName _framework;
        private readonly string _configuration;

        public ProjectAssemblyLoader(FrameworkName framework,
                                     string configuration,
                                     IAssemblyLoadContextAccessor loadContextAccessor,
                                     IProjectResolver projectResolver,
                                     ICompilationEngine compilationEngine)
        {
            _loadContextAccessor = loadContextAccessor;
            _compilationEngine = compilationEngine;
            _projectResolver = projectResolver;

            _framework = framework;
            _configuration = configuration;
        }

        public Assembly Load(AssemblyName assemblyName)
        {
            return Load(assemblyName, _loadContextAccessor.Default);
        }

        public Assembly Load(AssemblyName assemblyName, IAssemblyLoadContext loadContext)
        {
            // An assembly name like "MyLibrary!alternate!more-text"
            // is parsed into:
            // name == "MyLibrary"
            // aspect == "alternate"
            // and the more-text may be used to force a recompilation of an aspect that would
            // otherwise have been cached by some layer within Assembly.Load

            var name = assemblyName.Name;
            string aspect = null;
            var parts = name.Split(new[] { '!' }, 3);
            if (parts.Length != 1)
            {
                name = parts[0];
                aspect = parts[1];
            }

            Project project;
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            return _compilationEngine.LoadProject(
                project, 
                _framework,
                _configuration,
                aspect, 
                loadContext);
        }
    }
}