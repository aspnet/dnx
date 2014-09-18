// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.PackageManager
{
    public class DirTree
    {
        private JObject _json;
        private Dictionary<string, string> _pathToContents;

        public DirTree(string json)
        {
            _json = JObject.Parse(json);
            _pathToContents = new Dictionary<string, string>();
        }

        public DirTree WithFileContents(string relativePath, string contents)
        {
            _pathToContents.Add(relativePath, contents);
            return this;
        }

        public DirTree WriteTo(string rootDirPath)
        {
            Directory.CreateDirectory(rootDirPath);
            WriteToCore(_json, rootDirPath);

            foreach (var pair in _pathToContents)
            {
                var path = Path.Combine(rootDirPath, pair.Key);
                if (File.Exists(path))
                {
                    File.WriteAllText(path, pair.Value);
                }
                else
                {
                    var message = string.Format("'{0}' is not in created directory structure.", pair.Key);
                    throw new Exception(message);
                }
            }

            return this;
        }

        public bool MatchDirectoryOnDisk(string dirPath, bool compareFileContents = true)
        {
            // Flatten this directory structure into the dictionary _pathToContents
            FlattenJsonToDictionary();

            if (!string.IsNullOrEmpty(dirPath))
            {
                dirPath = dirPath[dirPath.Length - 1] == Path.DirectorySeparatorChar ?
                        dirPath : dirPath + Path.DirectorySeparatorChar;
            }

            var dirFileList = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories)
                .Select(x => x.Substring(dirPath.Length));

            if (_pathToContents.Count != dirFileList.Count())
            {
                Console.WriteLine("Number of files in '{0}' is {1}, while expected number is {2}.",
                    dirPath, dirFileList.Count(), _pathToContents.Count);
                return false;
            }

            foreach (var file in dirFileList)
            {
                if (!_pathToContents.ContainsKey(file))
                {
                    Console.WriteLine("Expecting '{0}', which doesn't exist in '{1}'", file, dirPath);
                    return false;
                }

                var fullPath = Path.Combine(dirPath, file);
                var onDiskFileContents = File.ReadAllText(fullPath);
                if (!string.Equals(onDiskFileContents, _pathToContents[file]))
                {
                    Console.WriteLine("The contents of '{0}' don't match expected contents.", fullPath);
                    Console.WriteLine("Expected:");
                    Console.WriteLine(_pathToContents[file]);
                    Console.WriteLine("Actual:");
                    Console.WriteLine(onDiskFileContents);
                    return false;
                }
            }

            return true;
        }

        private void FlattenJsonToDictionary()
        {
            FlattenJsonToDictionaryCore(_json, path:  string.Empty);
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

        private void WriteToCore(JObject dirObj, string path)
        {
            foreach (var property in dirObj.Properties())
            {
                // If value of the property is a string, the name of this property represents
                // a file and the value represents the contents of the file
                if (property.Value is JValue)
                {
                    File.WriteAllText(Path.Combine(path, property.Name), property.Value.ToString());
                }
                // If value of the property is an array, the name of this property represents
                // a directory and the value represents a list of empty files in the directory
                else if (property.Value is JArray)
                {
                    Directory.CreateDirectory(Path.Combine(path, property.Name));
                    foreach (var element in (property.Value as JArray))
                    {
                        var elementValue = (element as JValue).ToString();
                        File.WriteAllText(Path.Combine(path, property.Name, elementValue), string.Empty);
                    }
                }
                // If value of the property is an object, the name of this property represents
                // a directory and the value is processed recursively as a sub-directory
                else if (property.Value is JObject)
                {
                    Directory.CreateDirectory(Path.Combine(path, property.Name));
                    WriteToCore(property.Value as JObject, Path.Combine(path, property.Name));
                }
            }
        }
    }
}
