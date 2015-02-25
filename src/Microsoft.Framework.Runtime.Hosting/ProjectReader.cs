using System;
using System.IO;
using Microsoft.Framework.Runtime.Hosting.Internal;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Hosting
{
    public static class ProjectReader
    {
        public static readonly string ProjectFileName = "project.json";

        public static bool HasProjectFile(string directory)
        {
            string file = Path.Combine(directory, ProjectFileName);
            return FileSystem.FileExists(file);
        }

        public static bool HasLockFile(string directory)
        {
            string file = Path.Combine(directory, LockFileFormat.LockFileName);
            return FileSystem.FileExists(file);
        }

        public static Project ReadProjectFile(string directory)
        {
            string file = Path.Combine(directory, ProjectFileName);
            PackageSpec packageSpec;
            using (var stream = FileSystem.OpenRead(file))
            {
                packageSpec = JsonPackageSpecReader.GetPackageSpec(
                    stream,
                    GetDirectoryName(directory),
                    file);
            }
            return new Project(packageSpec);
        }

        public static LockFile ReadLockFile(string directory)
        {
            string file = Path.Combine(directory, LockFileFormat.LockFileName);
            using (var stream = FileSystem.OpenRead(file))
            {
                return LockFileFormat.Read(stream);
            }
        }

        private static string GetDirectoryName(string path)
        {
            path = path.TrimEnd(Path.DirectorySeparatorChar);
            return path.Substring(Path.GetDirectoryName(path).Length).Trim(Path.DirectorySeparatorChar);
        }
    }
}