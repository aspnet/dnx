// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class WalkContext
    {
        private readonly IDictionary<string, Item> _usedItems = new Dictionary<string, Item>();

        public void Walk(
            IEnumerable<IDependencyProvider> dependencyResolvers,
            string name,
            SemanticVersion version,
            FrameworkName frameworkName)
        {
            var root = new Node
            {
                Key = new LibraryRange(name, frameworkReference: false)
                {
                    VersionRange = new SemanticVersionRange(version)
                }
            };

            var resolvers = dependencyResolvers as IDependencyProvider[] ?? dependencyResolvers.ToArray();
            var resolvedItems = new Dictionary<LibraryRange, Item>();

            var buildTreeSw = Stopwatch.StartNew();

            // Recurse through dependencies optimistically, asking resolvers for dependencies
            // based on best match of each encountered dependency
            ForEach(root, node =>
            {
                node.Item = Resolve(resolvedItems, resolvers, node.Key, frameworkName);
                if (node.Item == null)
                {
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

                        if (eclipsed)
                        {
                            throw new InvalidOperationException(string.Format("Circular dependency detected {0}.", GetChain(node, dependency)));
                        }

                        foreach (var sideNode in scanNode.InnerNodes)
                        {
                            eclipsed |= string.Equals(
                                sideNode.Key.Name,
                                dependency.Name,
                                StringComparison.OrdinalIgnoreCase);

                            if (eclipsed)
                            {
                                break;
                            }
                        }
                    }

                    if (!eclipsed)
                    {
                        var innerNode = new Node
                        {
                            OuterNode = node,
                            Key = dependency.LibraryRange
                        };

                        node.InnerNodes.Add(innerNode);
                    }
                }
            });

            buildTreeSw.Stop();
            Logger.TraceInformation("[{0}]: Graph walk stage 1 took in {1}ms", GetType().Name, buildTreeSw.ElapsedMilliseconds);

            ForEach(root, true, (node, state) =>
            {
                if (!state || node.Item == null)
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

        private static string GetChain(Node node, LibraryDependency dependency)
        {
            var result = dependency.Name;
            var current = node;

            while (current != null)
            {
                result = current.Key.Name + " -> " + result;
                current = current.OuterNode;
            }

            return result;
        }

        private static void ForEach<TState>(Node root, TState state, Func<Node, TState, TState> visitor)
        {
            // breadth-first walk of Node tree

            var queue = new Queue<Tuple<Node, TState>>();
            queue.Enqueue(Tuple.Create(root, state));
            while (queue.Count > 0)
            {
                var work = queue.Dequeue();
                var innerState = visitor(work.Item1, work.Item2);
                foreach (var innerNode in work.Item1.InnerNodes)
                {
                    queue.Enqueue(Tuple.Create(innerNode, innerState));
                }
            }
        }

        private static void ForEach(Node root, Action<Node> visitor)
        {
            // breadth-first walk of Node tree, without TState parameter
            ForEach(root, 0, (node, _) =>
            {
                visitor(node);
                return 0;
            });
        }

        private Item Resolve(
            Dictionary<LibraryRange, Item> resolvedItems,
            IEnumerable<IDependencyProvider> providers,
            LibraryRange packageKey,
            FrameworkName frameworkName)
        {
            Item item;
            if (resolvedItems.TryGetValue(packageKey, out item))
            {
                return item;
            }

            Tuple<IDependencyProvider, LibraryDescription> hit = null;

            foreach (var dependencyProvider in providers)
            {
                var match = dependencyProvider.GetDescription(packageKey, frameworkName);
                if (match != null)
                {
                    hit = Tuple.Create(dependencyProvider, match);
                    break;
                }
            }

            if (hit == null)
            {
                resolvedItems[packageKey] = null;
                return null;
            }

            var provider = hit.Item1;
            var libraryDescripton = hit.Item2;

            if (resolvedItems.TryGetValue(libraryDescripton.Identity, out item))
            {
                return item;
            }

            item = new Item()
            {
                Description = libraryDescripton,
                Key = libraryDescripton.Identity,
                Dependencies = libraryDescripton.Dependencies,
                Resolver = provider,
            };

            resolvedItems[packageKey] = item;
            resolvedItems[libraryDescripton.Identity] = item;
            return item;
        }

        public void Populate(FrameworkName frameworkName, IList<LibraryDescription> libraries)
        {
            var sw = Stopwatch.StartNew();

            foreach (var groupByResolver in _usedItems.GroupBy(x => x.Value.Resolver))
            {
                var resolver = groupByResolver.Key;

                var descriptions = groupByResolver.Select(entry =>
                {
                    return new LibraryDescription
                    {
                        LibraryRange = entry.Value.Description.LibraryRange,
                        Identity = entry.Value.Key,
                        Path = entry.Value.Description.Path,
                        Type = entry.Value.Description.Type,
                        Framework = entry.Value.Description.Framework ?? frameworkName,
                        Dependencies = entry.Value.Dependencies.SelectMany(CorrectDependencyVersion).ToList(),
                        LoadableAssemblies = entry.Value.Description.LoadableAssemblies ?? Enumerable.Empty<string>(),
                        Resolved = entry.Value.Description.Resolved,
                        CompatibilityIssue = entry.Value.Description.CompatibilityIssue
                    };
                }).ToList();

                resolver.Initialize(descriptions, frameworkName, runtimeIdentifier: null);
                libraries.AddRange(descriptions);
            }

            sw.Stop();
            Logger.TraceInformation("[{0}]: Populate took {1}ms", GetType().Name, sw.ElapsedMilliseconds);
        }

        private IEnumerable<LibraryDependency> CorrectDependencyVersion(LibraryDependency dependency)
        {
            Item item;
            if (_usedItems.TryGetValue(dependency.Name, out item))
            {
                dependency.Library = item.Key;
                yield return dependency;
            }
        }

        private class Node
        {
            public Node()
            {
                InnerNodes = new List<Node>();
            }

            public LibraryRange Key { get; set; }
            public Item Item { get; set; }
            public Node OuterNode { get; set; }
            public IList<Node> InnerNodes { get; private set; }

            public override string ToString()
            {
                return (Item?.Key ?? Key).ToString();
            }
        }

        [DebuggerDisplay("{Key}")]
        private class Item
        {
            public LibraryDescription Description { get; set; }
            public LibraryIdentity Key { get; set; }
            public IDependencyProvider Resolver { get; set; }
            public IEnumerable<LibraryDependency> Dependencies { get; set; }
        }
    }
}