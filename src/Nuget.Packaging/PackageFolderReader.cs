using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    /// <summary>
    /// Reads an unzipped nupkg folder.
    /// </summary>
    public class PackageFolderReader : PackageReaderBase
    {
        private readonly DirectoryInfo _root;

        public PackageFolderReader(string folderPath)
            : this(new DirectoryInfo(folderPath))
        {

        }

        public PackageFolderReader(DirectoryInfo folder)
        {
            _root = folder;
        }

        /// <summary>
        /// Opens the nuspec file in read only mode.
        /// </summary>
        public override Stream GetNuspec()
        {
            FileInfo nuspecFile = _root.GetFiles("*.nuspec", SearchOption.TopDirectoryOnly).SingleOrDefault();

            if (nuspecFile == null)
            {
                throw new FileNotFoundException(String.Format(CultureInfo.CurrentCulture, Strings.MissingNuspec, _root.FullName));
            }

            return nuspecFile.OpenRead();
        }

        /// <summary>
        /// Opens a local file in read only mode.
        /// </summary>
        public override Stream GetStream(string path)
        {
            FileInfo file = new FileInfo(Path.Combine(_root.FullName, path));

            if (!file.FullName.StartsWith(_root.FullName, StringComparison.OrdinalIgnoreCase))
            {
                // the given path does not appear under the folder root
                throw new FileNotFoundException(path);
            }

            return file.OpenRead();
        }

        public override IEnumerable<string> GetFiles()
        {
            DirectoryInfo searchFolder = new DirectoryInfo(_root.FullName);

            foreach (FileInfo file in searchFolder.GetFiles("*", SearchOption.AllDirectories))
            {
                yield return GetRelativePath(_root, file);
            }

            yield break;
        }

        // TODO: add support for NuGet.ContentModel here
        protected override IEnumerable<string> GetFiles(string folder)
        {
            DirectoryInfo searchFolder = new DirectoryInfo(Path.Combine(_root.FullName, folder));

            if (searchFolder.Exists)
            {
                foreach (FileInfo file in searchFolder.GetFiles("*", SearchOption.AllDirectories))
                {
                    yield return GetRelativePath(_root, file);
                }
            }

            yield break;
        }

        /// <summary>
        /// Build the relative path in the same format that ZipArchive uses
        /// </summary>
        private static string GetRelativePath(DirectoryInfo root, FileInfo file)
        {
            Stack<DirectoryInfo> parents = new Stack<DirectoryInfo>();

            DirectoryInfo parent = file.Directory;

            while (parent != null && !StringComparer.OrdinalIgnoreCase.Equals(parent.FullName, root.FullName))
            {
                parents.Push(parent);
                parent = parent.Parent;
            }

            if (parent == null)
            {
                // the given file path does not appear under root
                throw new FileNotFoundException(file.FullName);
            }

            IEnumerable<string> parts = parents.Select(d => d.Name).Concat(new string[] { file.Name });

            return String.Join("/", parts);
        }
    }
}
