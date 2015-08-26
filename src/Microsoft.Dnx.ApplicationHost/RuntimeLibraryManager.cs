using System;
using System.Collections.Generic;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.ApplicationHost
{
    internal class RuntimeLibraryManager : ILibraryManager
    {
        private readonly Lazy<LibraryManager> _libraryManager;

        public RuntimeLibraryManager(ApplicationHostContext applicationHostContext)
        {
            _libraryManager = new Lazy<LibraryManager>(() =>
            {
                ApplicationHostContext.Initialize(applicationHostContext);
                return applicationHostContext.LibraryManager;
            });
        }

        public IEnumerable<Library> GetLibraries()
        {
            return _libraryManager.Value.GetLibraries();
        }

        public Library GetLibrary(string name)
        {
            return _libraryManager.Value.GetLibrary(name);
        }

        public IEnumerable<Library> GetReferencingLibraries(string name)
        {
            return _libraryManager.Value.GetReferencingLibraries(name);
        }
    }
}