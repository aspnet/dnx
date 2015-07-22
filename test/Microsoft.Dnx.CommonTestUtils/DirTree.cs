// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Dnx.CommonTestUtils
{
    public class DirTree
    {
        private Dictionary<string, string> _pathToContents;

        private DirTree(string jsonStr)
        {
            var json = JObject.Parse(jsonStr);
            _pathToContents = new Dictionary<string, string>();

            // Flatten this directory structure into the dictionary _pathToContents
            FlattenJsonToDictionary(json);
        }

        public static DirTree CreateFromJson(string jsonStr)
        {
            return new DirTree(jsonStr);
        }

        public static DirTree CreateFromDirectory(string dirPath)
        {
            var dirTree = CreateFromJson("{}");

            dirPath = EnsureTrailingSeparator(dirPath);

            var dirFileList = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories)
                .Select(x => x.Substring(dirPath.Length));

            foreach (var file in dirFileList)
            {
                var fullPath = Path.Combine(dirPath, file);
                var onDiskFileContents = File.ReadAllText(fullPath);
                dirTree._pathToContents[file] = onDiskFileContents;
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
            foreach (var pair in subDir._pathToContents)
            {
                var newPath = Path.Combine(relativePath, pair.Key);
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
            relativePath = EnsureTrailingSeparator(relativePath);
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
            dirPath = EnsureTrailingSeparator(dirPath);

            var dirFileList = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories)
                .Select(x => x.Substring(dirPath.Length));

            var missingFiles = _pathToContents.Keys.Except(dirFileList);
            var extraFiles = dirFileList.Except(_pathToContents.Keys);
            if (missingFiles.Any() || extraFiles.Any())
            {
                Console.Error.WriteLine("Number of files in '{0}' is {1}, while expected number is {2}.",
                    dirPath, dirFileList.Count(), _pathToContents.Count);
                Console.Error.WriteLine("Missing files: \n\n    " +
                    string.Join("\n    ", _pathToContents.Keys.Except(dirFileList)));
                Console.Error.WriteLine("Extra files: \n\n    " +
                    string.Join("\n    ", dirFileList.Except(_pathToContents.Keys)));
                Console.Error.WriteLine();
                return false;
            }

            if (compareFileContents)
            {
                foreach (var file in dirFileList)
                {
                    var fullPath = Path.Combine(dirPath, file);
                    var onDiskFileContents = File.ReadAllText(fullPath);
                    // Ignore new lines for compare
                    if (!string.Equals(onDiskFileContents.Replace("\r\n", "\n"), _pathToContents[file].Replace("\r\n", "\n")))
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

        private void FlattenJsonToDictionary(JObject json)
        {
            FlattenJsonToDictionaryCore(json, path: string.Empty);
        }

        private void FlattenJsonToDictionaryCore(JObject dirObj, string path)
        {
            foreach (var property in dirObj.Properties())
            {
                var relativePath = Path.Combine(path, string.Equals(property.Name, ".") ? string.Empty : property.Name);

                if (property.Value is JValue)
                {
                    // The contents specified with WithFileContents() override contents in original JSON
                    // If there are already contents for this file, the contents are specified by WithFileContents()
                    // So we ignore original contents here
                    if (!_pathToContents.ContainsKey(relativePath))
                    {
                        _pathToContents.Add(relativePath, property.Value.ToString());
                    }
                }
                else if (property.Value is JArray)
                {
                    foreach (var element in (property.Value as JArray))
                    {
                        var elementFilePath = Path.Combine(relativePath, (element as JValue).Value.ToString());
                        if (!_pathToContents.ContainsKey(elementFilePath))
                        {
                            _pathToContents.Add(elementFilePath, string.Empty);
                        }
                    }
                }
                else if (property.Value is JObject)
                {
                    FlattenJsonToDictionaryCore(property.Value as JObject, relativePath);
                }
            }
        }

        private static string EnsureTrailingSeparator(string dirPath)
        {
            if (!string.IsNullOrEmpty(dirPath))
            {
                dirPath = dirPath[dirPath.Length - 1] == Path.DirectorySeparatorChar ?
                        dirPath : dirPath + Path.DirectorySeparatorChar;
            }
            return dirPath;
        }
    }
}