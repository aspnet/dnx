using System;
using System.Collections.Generic;
using NuGet.LibraryModel;

namespace Microsoft.Framework.Runtime.Hosting
{
    /// <summary>
    /// Contains the collection of all resolved libraries for the application
    /// </summary>
    public class LibraryCollection
    {
        private HashSet<Library> _libraries;

        public IEnumerable<Library> Libraries => _libraries;

        public LibraryCollection(IEnumerable<Library> libraries)
        {
            _libraries = new HashSet<Library>(libraries);
        }
    }
}