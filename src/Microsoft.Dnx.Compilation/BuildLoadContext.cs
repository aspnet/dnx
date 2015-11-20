using System;
using System.IO;
using System.Reflection;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Runtime.Loader;

namespace Microsoft.Dnx.Compilation
{
    // This load context isn't tracked in the dictionary, it's a shim
    // so that we don't allocate a real load context until absolutely necessary
    internal class BuildLoadContext : LoadContext, IAssemblyLoadContext
    {
        private readonly Project _project;
        private readonly CompilationEngine _compilationEngine;
        private readonly string _configuration;
        private readonly CompilationEngineContext _compilationEngineContext;
        private RuntimeLoadContext _projectLoadContext;
        private readonly object _syncObject = new object();

        public BuildLoadContext(Project project,
                                CompilationEngine compilationEngine,
                                CompilationEngineContext compilationEngineContext,
                                string configuration)
        {
            _project = project;
            _compilationEngine = compilationEngine;
            _compilationEngineContext = compilationEngineContext;
            _configuration = configuration;
        }

        private RuntimeLoadContext LoadContext
        {
            get
            {
                lock (_syncObject)
                {
                    if (_projectLoadContext == null)
                    {
                        _projectLoadContext = InitializeProjectLoadContext();
                    }
                }

                return _projectLoadContext;
            }
        }

        Assembly IAssemblyLoadContext.Load(AssemblyName assemblyName)
        {
            try
            {
                return _compilationEngineContext.DefaultLoadContext.Load(assemblyName);
            }
            catch (FileNotFoundException)
            {
                return LoadContext.LoadWithoutDefault(assemblyName);
            }
        }

        public override Assembly LoadAssembly(AssemblyName assemblyName)
        {
            throw new NotImplementedException();
        }

        private RuntimeLoadContext InitializeProjectLoadContext()
        {
            // Create an application host context to use to drive a Load Context used to load Precompilers
            var context = new ApplicationHostContext
            {
                Project = _project,
                RuntimeIdentifiers = _compilationEngineContext.RuntimeEnvironment.GetAllRuntimeIdentifiers(),
                TargetFramework = _compilationEngineContext.ApplicationEnvironment.RuntimeFramework
            };

            var libraries = ApplicationHostContext.GetRuntimeLibraries(context);

            return new RuntimeLoadContext($"{_project.Name}_build", libraries, _compilationEngine, _compilationEngineContext.DefaultLoadContext, _configuration);
        }

        public override void Dispose()
        {
            _projectLoadContext?.Dispose();

            base.Dispose();
        }
    }
}
