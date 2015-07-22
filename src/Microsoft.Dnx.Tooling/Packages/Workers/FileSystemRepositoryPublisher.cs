// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Dnx.Tooling.Packages.Workers
{
    /// <summary>
    /// Summary description for FileSystemPackages
    /// </summary>
    public class FileSystemRepositoryPublisher : AbstractRepositoryPublisher
    {
        private readonly string _path;

        public FileSystemRepositoryPublisher(string path)
        {
            _path = path;
        }

        public override Stream ReadArtifactStream(string path)
        {
            var filePath = Path.Combine(_path, path);
            if (!File.Exists(filePath))
            {
                return null;
            }
            return new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete);
        }

        public override void WriteArtifactStream(string path, Stream content, bool createNew)
        {
            var combinedPath = Path.Combine(_path, path);
            var combinedDirectory = Path.GetDirectoryName(combinedPath);

            Directory.CreateDirectory(combinedDirectory);

            using (var stream = new FileStream(
                combinedPath,
                createNew ? FileMode.CreateNew : FileMode.Create,
                FileAccess.Write,
                FileShare.Delete))
            {
                content.CopyTo(stream);
            }
        }

        public override void RemoveArtifact(string path)
        {
            var combinedPath = Path.Combine(_path, path);
            if (File.Exists(combinedPath))
            {
                File.Delete(combinedPath);
            }
        }

        public override IEnumerable<string> EnumerateArtifacts(
            Func<string, bool> folderPredicate,
            Func<string, bool> artifactPredicate)
        {
            List<string> result = new List<string>();
            EnumerateArtifactsRecursive("", folderPredicate, artifactPredicate, result);
            return result;
        }

        void EnumerateArtifactsRecursive(
            string subPath,
            Func<string, bool> folderPredicate,
            Func<string, bool> artifactPredicate,
            List<string> result)
        {
            foreach (var name in Directory.EnumerateDirectories(Path.Combine(_path, subPath)))
            {
                var directoryName = new DirectoryInfo(name).Name;
                var directoryPath = Path.Combine(subPath, directoryName)
                    .Replace("\\", "/");
                if (folderPredicate(directoryPath))
                {
                    EnumerateArtifactsRecursive(
                        directoryPath,
                        folderPredicate,
                        artifactPredicate,
                        result);
                }
            }
            foreach (var name in Directory.EnumerateFiles(Path.Combine(_path, subPath)))
            {
                var fileName = Path.GetFileName(name);
                var filePath = Path.Combine(subPath, fileName)
                    .Replace("\\", "/");
                if (artifactPredicate(filePath))
                {
                    result.Add(filePath);
                }
            }
        }

    }
}