using System;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Summary description for LibraryKey
    /// </summary>
    internal static class LibraryKeyExtensions
    {
        public static ILibraryKey ChangeName(this ILibraryKey target, string name)
        {
            return new LibraryKey
            {
                Name = name,
                TargetFramework = target.TargetFramework,
                Configuration = target.Configuration,
                Aspect = target.Aspect,
            };
        }

        public static ILibraryKey ChangeTargetFramework(this ILibraryKey target, FrameworkName targetFramework)
        {
            return new LibraryKey
            {
                Name = target.Name,
                TargetFramework = targetFramework,
                Configuration = target.Configuration,
                Aspect = target.Aspect,
            };
        }

        public static ILibraryKey ChangeAspect(this ILibraryKey target, string aspect)
        {
            return new LibraryKey
            {
                Name = target.Name,
                TargetFramework = target.TargetFramework,
                Configuration = target.Configuration,
                Aspect = aspect,
            };
        }
    }
}