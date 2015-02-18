using System;

namespace NuGet.LibraryModel
{
    public static class LibraryExtensions
    {
        public static T GetProperty<T>(this Library library, string key)
        {
            object value;
            if (library.Properties.TryGetValue(key, out value))
            {
                return (T)value;
            }
            return default(T);
        }
    }
}