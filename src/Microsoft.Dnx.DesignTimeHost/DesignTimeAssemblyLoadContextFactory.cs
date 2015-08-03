// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Infrastructure;
using Microsoft.Dnx.Runtime.Loader;

namespace Microsoft.Dnx.DesignTimeHost
{
    internal class DesignTimeAssemblyLoadContextFactory : IAssemblyLoadContextFactory
    {
        private readonly Project _project;
        private readonly IApplicationEnvironment _appEnv;
        private readonly CompilationEngineFactory _compilationFactory;

        public DesignTimeAssemblyLoadContextFactory(Project project,
                                                    IApplicationEnvironment appEnv,
                                                    CompilationEngineFactory compilationFactory)
        {
            _project = project;
            _appEnv = appEnv;
            _compilationFactory = compilationFactory;
        }

        public IAssemblyLoadContext Create(IServiceProvider serviceProvider)
        {
            var hostContextKey = Tuple.Create("RuntimeLoadContext", _project.Name, _appEnv.Configuration, _appEnv.RuntimeFramework);
            
            var appHostContext = _compilationFactory.CompilationCache.Cache.Get<ApplicationHostContext>(hostContextKey, ctx => 
            {
                var applicationHostContext = new ApplicationHostContext(serviceProvider,
                                                                        _project.ProjectDirectory,
                                                                        packagesDirectory: null,
                                                                        configuration: _appEnv.Configuration,
                                                                        targetFramework: _appEnv.RuntimeFramework);

                applicationHostContext.DependencyWalker.Walk(_project.Name, _project.Version, _appEnv.RuntimeFramework);

                // Watch all projects for project.json changes
                foreach (var library in applicationHostContext.DependencyWalker.Libraries)
                {
                    if (string.Equals(library.Type, "Project"))
                    {
                        ctx.Monitor(new FileWriteTimeCacheDependency(library.Path));
                    }
                }

                // Add a cache dependency on restore complete to reevaluate dependencies
                ctx.Monitor(_compilationFactory.CompilationCache.NamedCacheDependencyProvider.GetNamedDependency(_project.Name + "_Dependencies"));
                
                return applicationHostContext;
            });

            var factory = new AssemblyLoadContextFactory(appHostContext.ServiceProvider);
            return factory.Create(appHostContext.ServiceProvider);
        }
    }
}