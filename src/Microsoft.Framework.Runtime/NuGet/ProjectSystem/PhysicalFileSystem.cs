// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Resources;

namespace NuGet
{
    public class PhysicalFileSystem : IFileSystem
    {
        private readonly string _root;
        private ILogger _logger;

        public PhysicalFileSystem(string root)
        {
            if (String.IsNullOrEmpty(root))
            {
                throw new ArgumentNullException("root");
            }
            _root = root;
        }

        public string Root
        {
            get
            {
                return _root;
            }
        }

        public ILogger Logger
        {
            get
            {
                return _logger ?? NullLogger.Instance;
            }
            set
            {
                _logger = value;
            }
        }

        public virtual string GetFullPath(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return Root;
            }
            return Path.Combine(Root, path);
        }

        public virtual void AddFile(string path, Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            AddFileCore(path, targetStream => stream.CopyTo(targetStream));
        }

        public virtual void AddFile(string path, Action<Stream> writeToStream)
        {
            if (writeToStream == null)
            {
                throw new ArgumentNullException("writeToStream");
            }

            AddFileCore(path, writeToStream);
        }

        private void AddFileCore(string path, Action<Stream> writeToStream)
        {
            EnsureDirectory(Path.GetDirectoryName(path));

            string fullPath = GetFullPath(path);

            using (Stream outputStream = File.Create(fullPath))
            {
                writeToStream(outputStream);
            }

            WriteAddedFileAndDirectory(path);
        }

        private void WriteAddedFileAndDirectory(string path)
        {
            string folderPath = Path.GetDirectoryName(path);

            if (!String.IsNullOrEmpty(folderPath))
            {
                Logger.Log(MessageLevel.Debug, NuGetResources.Debug_AddedFileToFolder, Path.GetFileName(path), folderPath);
            }
            else
            {
                Logger.Log(MessageLevel.Debug, NuGetResources.Debug_AddedFile, Path.GetFileName(path));
            }
        }

        public virtual void DeleteFile(string path)
        {
            if (!FileExists(path))
            {
                return;
            }

            try
            {
                MakeFileWritable(path);
                path = GetFullPath(path);
                File.Delete(path);
                string folderPath = Path.GetDirectoryName(path);
                if (!String.IsNullOrEmpty(folderPath))
                {
                    Logger.Log(MessageLevel.Debug, NuGetResources.Debug_RemovedFileFromFolder, Path.GetFileName(path), folderPath);
                }
                else
                {
                    Logger.Log(MessageLevel.Debug, NuGetResources.Debug_RemovedFile, Path.GetFileName(path));
                }
            }
            catch (FileNotFoundException)
            {

            }
        }

        public virtual void DeleteDirectory(string path)
        {
            DeleteDirectory(path, recursive: false);
        }

        public virtual void DeleteDirectory(string path, bool recursive)
        {
            if (!DirectoryExists(path))
            {
                return;
            }

            try
            {
                path = GetFullPath(path);
                Directory.Delete(path, recursive);

                // The directory is not guaranteed to be gone since there could be
                // other open handles. Wait, up to half a second, until the directory is gone.
                for (int i = 0; Directory.Exists(path) && i < 5; ++i)
                {
                    System.Threading.Thread.Sleep(100);
                }

                Logger.Log(MessageLevel.Debug, NuGetResources.Debug_RemovedFolder, path);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }

        public virtual IEnumerable<string> GetFiles(string path, bool recursive)
        {
            return GetFiles(path, null, recursive);
        }

        public virtual IEnumerable<string> GetFiles(string path, string filter, bool recursive)
        {
            path = PathUtility.EnsureTrailingSlash(GetFullPath(path));
            if (String.IsNullOrEmpty(filter))
            {
                filter = "*.*";
            }
            try
            {
                if (!Directory.Exists(path))
                {
                    return Enumerable.Empty<string>();
                }
                return Directory.EnumerateFiles(path, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                                .Select(MakeRelativePath);
            }
            catch (UnauthorizedAccessException)
            {

            }
            catch (DirectoryNotFoundException)
            {

            }

            return Enumerable.Empty<string>();
        }

        public virtual IEnumerable<string> GetDirectories(string path)
        {
            try
            {
                path = PathUtility.EnsureTrailingSlash(GetFullPath(path));
                if (!Directory.Exists(path))
                {
                    return Enumerable.Empty<string>();
                }
                return Directory.EnumerateDirectories(path)
                                .Select(MakeRelativePath);
            }
            catch (UnauthorizedAccessException)
            {

            }
            catch (DirectoryNotFoundException)
            {

            }

            return Enumerable.Empty<string>();
        }

        public IFileSystem GetDirectory(string path)
        {
            var fullPath = GetFullPath(path);

            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException();
            }

            return new PhysicalFileSystem(fullPath);
        }

        public virtual DateTimeOffset GetLastModified(string path)
        {
            path = GetFullPath(path);
            if (File.Exists(path))
            {
                return File.GetLastWriteTime(path).ToUniversalTime();
            }
            return Directory.GetLastWriteTime(path).ToUniversalTime();
        }

        public DateTimeOffset GetCreated(string path)
        {
            path = GetFullPath(path);
            if (File.Exists(path))
            {
                return File.GetCreationTime(path).ToUniversalTime();
            }
            return Directory.GetCreationTime(path).ToUniversalTime();
        }

        public DateTimeOffset GetLastAccessed(string path)
        {
            path = GetFullPath(path);
            if (File.Exists(path))
            {
                return File.GetLastAccessTime(path).ToUniversalTime();
            }
            return Directory.GetLastAccessTime(path).ToUniversalTime();
        }

        public virtual bool FileExists(string path)
        {
            path = GetFullPath(path);
            return File.Exists(path);
        }

        public virtual bool DirectoryExists(string path)
        {
            path = GetFullPath(path);
            return Directory.Exists(path);
        }

        public virtual Stream OpenFile(string path)
        {
            path = GetFullPath(path);
            return File.OpenRead(path);
        }

        public virtual Stream CreateFile(string path)
        {
            string fullPath = GetFullPath(path);

            // before creating the file, ensure the parent directory exists first.
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            WriteAddedFileAndDirectory(path);

            return File.Create(fullPath);
        }

        protected string MakeRelativePath(string fullPath)
        {
            return fullPath.Substring(Root.Length).TrimStart(Path.DirectorySeparatorChar);
        }

        protected virtual void EnsureDirectory(string path)
        {
            path = GetFullPath(path);
            Directory.CreateDirectory(path);
        }

        public void MakeFileWritable(string path)
        {
            path = GetFullPath(path);
            FileAttributes attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
        }

        public virtual void MoveFile(string source, string destination)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (destination == null)
            {
                throw new ArgumentNullException("destination");
            }
            string srcFull = GetFullPath(source);
            string destFull = GetFullPath(destination);

            if (string.Equals(srcFull, destFull, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            File.Move(srcFull, destFull);
        }
    }
}
