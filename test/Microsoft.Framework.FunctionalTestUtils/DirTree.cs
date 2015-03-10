// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Framework.FunctionalTestUtils
{
    public class DirTree
    {
        private Dictionary<string, string> _pathToContents;

        private DirTree(params string[] fileRelativePaths)
        {
            _pathToContents = fileRelativePaths.ToDictionary(f => f, _ => string.Empty);
        }

        public static DirTree CreateFromList(params string[] fileRelativePaths)
        {
            return new DirTree(fileRelativePaths);
        }

        public static DirTree CreateFromDirectory(string dirPath)
        {
            var dirTree = new DirTree();

            dirPath = EnsureTrailingForwardSlash(dirPath);

            var dirFileList = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories)
                .Select(x => x.Substring(dirPath.Length))
                .Select(x => GetPathWithForwardSlashes(x));

            // If we only generate a list of files, empty dirs will be left out
            // So we make an empty dir with trailing forward slash (e.g. "path/to/dir/") and put it into the list
            var dirEmptySubDirList = Directory.GetDirectories(dirPath, "*", SearchOption.AllDirectories)
                .Where(x => !Directory.GetFileSystemEntries(x).Any())
                .Select(x => x.Substring(dirPath.Length))
                .Select(x => GetPathWithForwardSlashes(x))
                .Select(x => EnsureTrailingForwardSlash(x));

            foreach (var file in dirFileList)
            {
                var fullPath = Path.Combine(dirPath, file);
                var onDiskFileContents = File.ReadAllText(fullPath);
                dirTree._pathToContents[file] = onDiskFileContents;
            }

            foreach (var emptyDir in dirEmptySubDirList)
            {
                // Empty dirs don't have contents
                dirTree._pathToContents[emptyDir] = null;
            }

            return dirTree;
        }

        public DirTree WithFileContents(string relativePath, string contents)
        {
            _pathToContents[relativePath] = contents;
            return this;
        }

        public DirTree WithFileContents(string relativePath, string contentsFormat, params object[] args)
        {
            _pathToContents[relativePath] = string.Format(contentsFormat, args);
            return this;
        }

        public DirTree WithSubDir(string relativePath, DirTree subDir)
        {
            // Append a DirTree as a subdir of current DirTree
            foreach (var pair in subDir._pathToContents)
            {
                var newPath = GetPathWithForwardSlashes(Path.Combine(relativePath, pair.Key));
                _pathToContents[newPath] = pair.Value;
            }
            return this;
        }

        public DirTree RemoveFile(string relativePath)
        {
            _pathToContents.Remove(relativePath);
            return this;
        }

        public DirTree RemoveSubDir(string relativePath)
        {
            relativePath = EnsureTrailingForwardSlash(relativePath);
            var removedKeys = new List<string>();
            foreach (var pair in _pathToContents)
            {
                if (pair.Key.StartsWith(relativePath))
                {
                    removedKeys.Add(pair.Key);
                }
            }
            foreach (var removedKey in removedKeys)
            {
                _pathToContents.Remove(removedKey);
            }
            return this;
        }

        public DirTree WriteTo(string rootDirPath)
        {
            foreach (var pair in _pathToContents)
            {
                var path = Path.Combine(rootDirPath, pair.Key);

                if (path.EndsWith("/") && !Directory.Exists(path))
                {
                    // Create an empty dir, which is represented as "path/to/dir"
                    Directory.CreateDirectory(path);
                    continue;
                }

                var parentDir = Path.GetDirectoryName(path);

                if (!Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                File.WriteAllText(path, pair.Value);
            }

            return this;
        }

        public bool MatchDirectoryOnDisk(string dirPath, bool compareFileContents = true)
        {
            dirPath = EnsureTrailingForwardSlash(dirPath);

            var dirFileList = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories)
                .Select(x => x.Substring(dirPath.Length))
                .Select(x => GetPathWithForwardSlashes(x));

            var dirEmptySubDirList = Directory.GetDirectories(dirPath, "*", SearchOption.AllDirectories)
                .Where(x => !Directory.GetFileSystemEntries(x).Any())
                .Select(x => x.Substring(dirPath.Length))
                .Select(x => GetPathWithForwardSlashes(x))
                .Select(x => EnsureTrailingForwardSlash(x));

            var expectedFileList = _pathToContents.Keys.Where(x => !x.EndsWith("/"));
            var expectedEmptySubDirList = _pathToContents.Keys.Where(x => x.EndsWith("/"));

            var missingFiles = expectedFileList.Except(dirFileList);
            var extraFiles = dirFileList.Except(expectedFileList);
            var missingEmptySubDirs = expectedEmptySubDirList.Except(dirEmptySubDirList);
            var extraEmptySubDirs = dirEmptySubDirList.Except(expectedEmptySubDirList);

            if (missingFiles.Any() || extraFiles.Any() || missingEmptySubDirs.Any() || extraEmptySubDirs.Any())
            {
                Console.Error.WriteLine("The structure of '{0}' doesn't match expected output structure.", dirPath);
                Console.Error.WriteLine("Missing items: " +
                    string.Join(",", missingFiles.Concat(missingEmptySubDirs)));
                Console.Error.WriteLine("Extra items: " +
                    string.Join(",", extraFiles.Concat(extraEmptySubDirs)));
                return false;
            }

            if (compareFileContents)
            {
                foreach (var file in dirFileList)
                {
                    var fullPath = Path.Combine(dirPath, file);
                    var onDiskFileContents = File.ReadAllText(fullPath);
                    if (!string.Equals(onDiskFileContents, _pathToContents[file]))
                    {
                        Console.Error.WriteLine("The contents of '{0}' don't match expected contents.", fullPath);
                        Console.Error.WriteLine("Expected:");
                        Console.Error.WriteLine(_pathToContents[file]);
                        Console.Error.WriteLine("Actual:");
                        Console.Error.WriteLine(onDiskFileContents);
                        return false;
                    }
                }
            }

            return true;
        }

        private static string EnsureTrailingForwardSlash(string dirPath)
        {
            if (!string.IsNullOrEmpty(dirPath))
            {
                dirPath = dirPath[dirPath.Length - 1] == '/' ? dirPath : dirPath + '/';
            }
            return dirPath;
        }

        private static string GetPathWithForwardSlashes(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}