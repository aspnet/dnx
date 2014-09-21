using NuGet;
using System;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Summary description for LibraryDescriptor
    /// </summary>
    public class LibraryDependency
    {
        public LibraryDependency(string name)
        {
            Library = new Library
            {
                Name = name
            };
        }

        public LibraryDependency(string name, bool isGacOrFrameworkReference)
        {
            Library = new Library
            {
                Name = name,
                IsGacOrFrameworkReference = isGacOrFrameworkReference
            };
        }

        public LibraryDependency(string name, SemanticVersion version)
        {
            Library = new Library
            {
                Name = name,
                Version = version
            };
        }

        public LibraryDependency(string name, SemanticVersion version, bool isGacOrFrameworkReference)
        {
            Library = new Library
            {
                Name = name,
                Version = version,
                IsGacOrFrameworkReference = isGacOrFrameworkReference
            };
        }

        public LibraryDependency(Library library)
        {
            Library = library;
        }

        public Library Library { get; set; }

        public string Name
        {
            get { return Library.Name; }
        }

        public SemanticVersion Version
        {
            get { return Library.Version; }
        }

        public LibraryDependency ChangeVersion(SemanticVersion version)
        {
            return new LibraryDependency(
                name: Name, 
                version: version);
        }
    }
}