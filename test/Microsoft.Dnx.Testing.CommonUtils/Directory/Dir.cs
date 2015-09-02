using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Dnx.Testing
{
    public class Dir
    {
        public const string EmptyFile = "";
        private readonly Dictionary<string, object> _nodes = new Dictionary<string, object>();

        public Dir(string rootPath): this()
        {
            Load(rootPath);
        }

        public Dir()
        {
            ReadFile = fileInfo => File.ReadAllText(fileInfo.FullName);
        }

        public Dir(Dir parent)
        {
            ReadFile = parent.ReadFile;
        }

        public Func<FileInfo, object> ReadFile;

        public string LoadPath { get; private set; } = "In Memory";

        public IEnumerable<KeyValuePair<string, object>> Nodes
        {
            get
            {
                return _nodes;
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

        public void Load(string path)
        {
            LoadPath = path;
            var directory = new DirectoryInfo(path);

            foreach (var subDirectoryInfo in directory.EnumerateDirectories())
            {
                Read(this, subDirectoryInfo);
            }

            foreach (var file in directory.EnumerateFiles())
            {
                this[file.Name] = ReadFile(file);
            }
        }

        public void Save(string path)
        {
            Write(path, this);
        }

        private void Write(string path, object value)
        {
            var tree = value as Dir;
            if (tree != null)
            {
                Directory.CreateDirectory(path);

                foreach (var node in tree.Nodes)
                {
                    Write(Path.Combine(path, node.Key), node.Value);
                }
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, value?.ToString() ?? string.Empty);
            }
        }

        private static void Read(Dir parent, DirectoryInfo directoryInfo)
        {
            var directory = new Dir(parent);

            foreach (var subDirectoryInfo in directoryInfo.EnumerateDirectories())
            {
                Read(directory, subDirectoryInfo);
            }

            foreach (var file in directoryInfo.EnumerateFiles())
            {
                directory[file.Name] = directory.ReadFile(file);
            }

            parent[directoryInfo.Name] = directory;
        }

        public DirDiff Diff(Dir other)
        {
            var nodes1 = Flatten().ToDictionary(pair => pair.Key, pair => pair.Value);
            var nodes2 = other.Flatten().ToDictionary(pair => pair.Key, pair => pair.Value);

            return new DirDiff
            {
                ExtraEntries = nodes1.Keys.Except(nodes2.Keys),
                MissingEntries = nodes2.Keys.Except(nodes1.Keys),
                DifferentEntries = nodes1.Keys.Intersect(nodes2.Keys)
                    .Where(entry => !string.Equals(nodes1[entry].ToString(), nodes2[entry].ToString(), StringComparison.Ordinal))
            };
        }

        public IEnumerable<KeyValuePair<string, object>> Flatten()
        {
            var allNodes = new Dictionary<string, object>();
            var stack = new Stack<Tuple<string, Dir>>();
            stack.Push(Tuple.Create(string.Empty, this));

            while (stack.Count > 0)
            {
                var top = stack.Pop();
                var basePath = top.Item1;
                var tree = top.Item2;

                foreach (var node in tree.Nodes)
                {
                    var subTree = node.Value as Dir;
                    var subPath = string.IsNullOrEmpty(basePath) ? node.Key : $"{basePath}/{node.Key}";
                    if (subTree == null)
                    {
                        allNodes[subPath] = node.Value;
                    }
                    else
                    {
                        stack.Push(Tuple.Create(subPath, subTree));
                    }
                }
            }

            return allNodes;
        }
    }
}
