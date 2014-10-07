// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class WalkContext
    {
        private readonly IDictionary<string, Item> _usedItems = new Dictionary<string, Item>();

        void ForEach<TState>(Node root, TState state, Func<Node, TState, TState> visitor)
        {
            // breadth-first walk of Node tree

            var queue = new Queue<Tuple<Node, TState>>();
            var patience = 10000;
            queue.Enqueue(Tuple.Create(root, state));
            while (!queue.IsEmpty() && --patience != 0)
            {
                var work = queue.Dequeue();
                var innerState = visitor(work.Item1, work.Item2);
                foreach (var innerNode in work.Item1.InnerNodes)
                {
                    queue.Enqueue(Tuple.Create(innerNode, innerState));
                }
            }
        }

        private void ForEach(Node root, Action<Node> visitor)
        {
            // breadth-first walk of Node tree, without TState parameter
            ForEach(root, 0, (node, _) =>
            {
                visitor(node);
                return 0;
            });
        }

        public void Walk(
            IEnumerable<IDependencyProvider> dependencyResolvers,
            string name,
            SemanticVersion version,
            FrameworkName frameworkName)
        {
            var root = new Node
            {
                Key = new Library { Name = name, Version = version }
            };

            var resolvers = dependencyResolvers as IDependencyProvider[] ?? dependencyResolvers.ToArray();
            var resolvedItems = new Dictionary<Library, Item>();

            // Recurse through dependencies optimistically, asking resolvers for dependencies
            // based on best match of each encountered dependency
            ForEach(root, node =>
            {
                node.Item = Resolve(resolvedItems, resolvers, node.Key, frameworkName);
                if (node.Item == null)
                {
                    node.Disposition = Disposition.Rejected;
                    return;
                }

                foreach (var dependency in node.Item.Dependencies)
                {
                    // determine if a child dependency is eclipsed by
                    // a reference on the line leading to this point. this
                    // prevents cyclical dependencies, and also implements the
                    // "nearest wins" rule.

                    var eclipsed = false;
                    for (var scanNode = node;
                         scanNode != null && !eclipsed;
                         scanNode = scanNode.OuterNode)
                    {
                        eclipsed |= string.Equals(
                            scanNode.Key.Name,
                            dependency.Name,
                            StringComparison.OrdinalIgnoreCase);

                        foreach (var sideNode in scanNode.InnerNodes)
                        {
                            eclipsed |= string.Equals(
                                sideNode.Key.Name,
                                dependency.Name,
                                StringComparison.OrdinalIgnoreCase);
                        }
                    }

                    if (!eclipsed)
                    {
                        var innerNode = new Node
                        {
                            OuterNode = node,
                            Key = dependency.Library,
                        };
                        node.InnerNodes.Add(innerNode);
                    }
                }
            });

            // now we walk the tree as often as it takes to determine 
            // which paths are accepted or rejected, based on conflicts occuring
            // between cousin packages

            var patience = 1000;
            var incomplete = true;
            while (incomplete && --patience != 0)
            {
                // Create a picture of what has not been rejected yet
                var tracker = new Tracker();
                ForEach(root, true, (node, state) =>
                {
                    if (!state || node.Disposition == Disposition.Rejected)
                    {
                        return false;
                    }
                    tracker.Track(node.Item);
                    return true;
                });

                // Inform tracker of ambiguity beneath nodes that are not resolved yet
                // between:
                // a1->b1->d1->x1
                // a1->c1->d2->z1
                // first attempt
                //  d1/d2 are considered disputed 
                //  x1 and z1 are considered ambiguous
                //  d1 is rejected
                // second attempt
                //  d1 is rejected, d2 is accepted
                //  x1 is no longer seen, and z1 is not ambiguous
                //  z1 is accepted

                ForEach(root, "Walking", (node, state) =>
                {
                    if (node.Disposition == Disposition.Rejected)
                    {
                        return "Rejected";
                    }
                    if (state == "Walking" && tracker.IsDisputed(node.Item))
                    {
                        return "Ambiguous";
                    }
                    if (state == "Ambiguous")
                    {
                        tracker.MarkAmbiguous(node.Item);
                    }
                    return state;
                });

                // Now mark unambiguous nodes as accepted or rejected
                ForEach(root, true, (node, state) =>
                {
                    if (!state || node.Disposition == Disposition.Rejected)
                    {
                        return false;
                    }
                    if (tracker.IsAmbiguous(node.Item))
                    {
                        return false;
                    }
                    if (node.Disposition == Disposition.Acceptable)
                    {
                        node.Disposition = tracker.IsBestVersion(node.Item) ? Disposition.Accepted : Disposition.Rejected;
                    }
                    return node.Disposition == Disposition.Accepted;
                });

                incomplete = false;
                ForEach(root, node => incomplete |= node.Disposition == Disposition.Acceptable);

                // uncomment in case of emergencies: TraceState(root);
            }

            ForEach(root, true, (node, state) =>
            {
                if (state == false ||
                    node.Disposition != Disposition.Accepted ||
                    node.Item == null)
                {
                    return false;
                }

                if (!_usedItems.ContainsKey(node.Item.Key.Name))
                {
                    _usedItems[node.Item.Key.Name] = node.Item;
                }
                return true;
            });

            // uncomment in case of emergencies: TraceState(root);
        }

        private void TraceState(Node root)
        {
            var elt = new XElement("state");
            ForEach(root, elt, (node, parent) =>
            {
                var child = new XElement(node.Key.Name,
                    new XAttribute("version", node.Key.Version == null ? "null" : node.Key.Version.ToString()),
                    new XAttribute("disposition", node.Disposition.ToString()));
                parent.Add(child);
                return child;
            });

            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, IndentChars = "  " }))
            {
                elt.WriteTo(writer);
            }
            Trace.TraceInformation("[{0}] Current State\r\n{1}", GetType().Name, sb);
        }

        public enum Disposition
        {
            Acceptable,
            Rejected,
            Accepted
        }

        private Item Resolve(
            Dictionary<Library, Item> resolvedItems,
            IEnumerable<IDependencyProvider> resolvers,
            Library packageKey,
            FrameworkName frameworkName)
        {
            Item item;
            if (resolvedItems.TryGetValue(packageKey, out item))
            {
                return item;
            }

            var hit = resolvers
                .Select(x => new
                {
                    Resolver = x,
                    Details = x.GetDescription(packageKey, frameworkName)
                })
                .FirstOrDefault(x => x.Details != null);

            if (hit == null)
            {
                resolvedItems[packageKey] = null;
                return null;
            }

            if (resolvedItems.TryGetValue(hit.Details.Identity, out item))
            {
                return item;
            }

            item = new Item()
            {
                Description = hit.Details,
                Key = hit.Details.Identity,
                Dependencies = hit.Details.Dependencies,
                Resolver = hit.Resolver,
            };
            resolvedItems[packageKey] = item;
            resolvedItems[hit.Details.Identity] = item;
            return item;
        }

        public void Populate(FrameworkName frameworkName, IList<LibraryDescription> libraries)
        {
            foreach (var groupByResolver in _usedItems.GroupBy(x => x.Value.Resolver))
            {
                var resolver = groupByResolver.Key;
                var packageKeys = groupByResolver.Select(x => x.Value.Key).ToList();

                Trace.TraceInformation("[{0}]: " + String.Join(", ", packageKeys), resolver.GetType().Name);

                var descriptions = groupByResolver.Select(entry =>
                {
                    return new LibraryDescription
                    {
                        Identity = entry.Value.Key,
                        Path = entry.Value.Description.Path,
                        Type = entry.Value.Description.Type,
                        Framework = entry.Value.Description.Framework ?? frameworkName,
                        Dependencies = entry.Value.Dependencies.SelectMany(CorrectDependencyVersion).ToList()
                    };
                }).ToList();

                resolver.Initialize(descriptions, frameworkName);
                libraries.AddRange(descriptions);
            }
        }

        private IEnumerable<LibraryDependency> CorrectDependencyVersion(LibraryDependency dependency)
        {
            Item item;
            if (_usedItems.TryGetValue(dependency.Name, out item))
            {
                yield return dependency.ChangeVersion(item.Key.Version);
            }
        }

        public class Node
        {
            public Node()
            {
                InnerNodes = new List<Node>();
                Disposition = Disposition.Acceptable;
            }

            public Library Key { get; set; }
            public Item Item { get; set; }
            public Node OuterNode { get; set; }
            public IList<Node> InnerNodes { get; private set; }

            public Disposition Disposition { get; set; }
        }

        public class Item
        {
            public LibraryDescription Description { get; set; }
            public Library Key { get; set; }
            public IDependencyProvider Resolver { get; set; }
            public IEnumerable<LibraryDependency> Dependencies { get; set; }
        }

        public class Tracker
        {
            class Entry
            {
                public Entry()
                {
                    List = new List<Item>();
                }

                public List<Item> List { get; set; }

                public bool Ambiguous { get; set; }
            }

            readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

            private Entry GetEntry(Item item)
            {
                Entry itemList;
                if (!_entries.TryGetValue(item.Key.Name, out itemList))
                {
                    itemList = new Entry();
                    _entries[item.Key.Name] = itemList;
                }
                return itemList;
            }

            public void Track(Item item)
            {
                var entry = GetEntry(item);
                if (!entry.List.Contains(item))
                {
                    entry.List.Add(item);
                }
            }

            public bool IsDisputed(Item item)
            {
                return GetEntry(item).List.Count > 1;
            }

            public bool IsAmbiguous(Item item)
            {
                return GetEntry(item).Ambiguous;
            }

            public void MarkAmbiguous(Item item)
            {
                GetEntry(item).Ambiguous = true;
            }

            public bool IsBestVersion(Item item)
            {
                var entry = GetEntry(item);
                return entry.List.All(known => item.Key.Version >= known.Key.Version);
            }
        }
    }
}