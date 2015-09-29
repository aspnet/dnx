// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Dnx.Testing.Framework
{
    public class Dir
    {
        public const string EmptyFile = "";

        private readonly Dictionary<string, object> _nodes = new Dictionary<string, object>();
        private Func<FileInfo, object> _readFile;
        private Func<object, object, bool> _fileComparator;

        public Dir(string rootPath)
            : this()
        {
            if (!Directory.Exists(rootPath))
            {
                throw new ArgumentException("Directory was not found", nameof(rootPath));
            }

            LoadPath = rootPath;
            Load(rootPath);
        }

        public Dir()
        {
            _readFile = fileInfo => new Lazy<object>(() => File.ReadAllText(fileInfo.FullName));
            _fileComparator = DefaultFileComparator;
        }

        public Dir(Dir parent)
        {
            _readFile = parent._readFile;
            _fileComparator = parent._fileComparator;
        }

        public string LoadPath { get; } = "In Memory";

        public object this[string key]
        {
            get
            {
                if (_nodes[key] is Lazy<object>)
                {
                    _nodes[key] = ((Lazy<object>)_nodes[key])?.Value;
                }
                return _nodes[key];
            }
            set
            {
                _nodes[key] = value;
            }
        }

        public object this[params string[] keys]
        {
            set
            {
                foreach (var key in keys)
                {
                    _nodes[key] = value;
                }
            }
        }

        public Dictionary<string, object> GetNodes(bool loadFiles = true)
        {
            if (loadFiles)
            {
                var keys = new List<string>(_nodes.Keys);
                foreach (var key in keys)
                {
                    if (_nodes[key] is Lazy<object>)
                    {
                        _nodes[key] = ((Lazy<object>)_nodes[key])?.Value;
                    }
                }
            }
            return _nodes;
        }

        public void Load(string path)
        {
            var directory = new DirectoryInfo(path);

            foreach (var subDirectoryInfo in directory.EnumerateDirectories())
            {
                Dir subDirectory = new Dir(this);
                this[subDirectoryInfo.Name] = subDirectory;
                subDirectory.Load(Path.Combine(path, subDirectoryInfo.Name));
            }

            foreach (var file in directory.EnumerateFiles())
            {
                this[file.Name] = _readFile(file);
            }
        }

        public void Save(string path)
        {
            Write(path, this);
        }

        public DirDiff Diff(Dir other, bool compareContents = true)
        {
            var nodes1 = Flatten(compareContents);
            var nodes2 = other.Flatten(compareContents);

            return new DirDiff
            {
                ExtraEntries = nodes1.Keys.Except(nodes2.Keys),
                MissingEntries = nodes2.Keys.Except(nodes1.Keys),
                DifferentEntries = compareContents ? 
                    nodes1.Keys.Intersect(nodes2.Keys).Where(
                        entry => _fileComparator(nodes1[entry], nodes2[entry])) :
                        Enumerable.Empty<string>()

            };
        }

        public Dictionary<string, object> Flatten(bool loadFiles = true)
        {
            var allNodes = new Dictionary<string, object>();
            var stack = new Stack<Tuple<string, Dir, bool>>();
            stack.Push(Tuple.Create(string.Empty, this, false));

            while (stack.Count > 0)
            {
                var top = stack.Pop();
                var basePath = top.Item1;
                var tree = top.Item2;
                var skipComparison = top.Item3;

                foreach (var node in tree.GetNodes(loadFiles))
                {
                    var dirItem = node.Value as DirItem;
                    var skipItem = skipComparison || (dirItem == null ? false : dirItem.SkipComparison );

                    var subTree = dirItem == null ? node.Value as Dir : dirItem.Item as Dir;
                    var subPath = string.IsNullOrEmpty(basePath) ? node.Key : $"{basePath}/{node.Key}";
                    if (subTree == null)
                    {
                        if (skipItem && dirItem != null)
                        {
                            dirItem.SkipComparison = true;
                        }

                        allNodes[subPath] = skipItem ? (dirItem == null ? new DirItem(node.Value, true) : dirItem) : node.Value;
                    }
                    else
                    {
                        stack.Push(Tuple.Create(subPath, subTree, skipItem));
                    }
                }
            }

            return allNodes;
        }

        private bool DefaultFileComparator(object file1, object file2)
        {
            var item1 = file1 as DirItem;
            var item2 = file2 as DirItem;

            if (item1 != null)
            {
                if (item1.SkipComparison)
                {
                    return false;
                }
                file1 = item1.Item;
            }

            if (item2 != null)
            {
                if (item2.SkipComparison)
                {
                    return false;
                }
                file2 = item2.Item;
            }

            return !string.Equals(file1.ToString(), file2.ToString(), StringComparison.Ordinal);
        }

        private void Write(string path, object value)
        {
            var tree = value as Dir;
            if (tree != null)
            {
                Directory.CreateDirectory(path);

                foreach (var node in tree.GetNodes(loadFiles: true))
                {
                    Write(Path.Combine(path, node.Key), node.Value);
                }
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, value.ToString() ?? string.Empty);
            }
        }
    }
}
