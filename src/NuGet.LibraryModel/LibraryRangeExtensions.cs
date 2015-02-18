using System;

namespace NuGet.LibraryModel
{
    public static class LibraryRangeExtensions
    {
        public static bool IsEclipsedBy(this LibraryRange library, LibraryRange other)
        {
            return string.Equals(library.Name, other.Name, StringComparison.OrdinalIgnoreCase) && 
                   string.Equals(library.Type, other.Type);
        }
    }
}