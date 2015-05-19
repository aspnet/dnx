// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;
using NuGet;
using Xunit;

namespace Loader.Tests
{
    public class DependencyWalkerFacts
    {
        LibraryDependency[] Dependencies(Action<TestDependencyProvider.Entry> configure)
        {
            var entry = new TestDependencyProvider.Entry();
            configure(entry);
            return entry.Dependencies.ToArray();
        }

        private void AssertDependencies(
            IEnumerable<LibraryDependency> actual,
            Action<TestDependencyProvider.Entry> expected)
        {
            var builder = new TestDependencyProvider.Entry();
            expected(builder);

            Assert.Equal(builder.Dependencies.Select(d => d.Library), actual.Select(d => d.Library));
        }

        [Fact]
        public void SimpleGraphCanBeWalked()
        {
            var testProvider = new TestDependencyProvider()
                .Package("a", "1.0", that => that.Needs("b", "1.0").Needs("c", "1.0"))
                .Package("b", "1.0")
                .Package("c", "1.0");

            var walker = new DependencyWalker(new[] { testProvider });
            walker.Walk("a", new SemanticVersion("1.0"), VersionUtility.ParseFrameworkName("net45"));

            AssertDependencies(testProvider.Dependencies, that => that
                 .Needs("a", "1.0")
                 .Needs("b", "1.0")
                 .Needs("c", "1.0"));
        }


        [Fact]
        public void NestedGraphCanBeWalked()
        {
            var testProvider = new TestDependencyProvider()
                .Package("a", "1.0", that => that.Needs("b", "1.0").Needs("c", "1.0"))
                .Package("b", "1.0", that => that.Needs("c", "1.0").Needs("d", "1.0"))
                .Package("c", "1.0")
                .Package("d", "1.0");

            var walker = new DependencyWalker(new[] { testProvider });
            walker.Walk("a", new SemanticVersion("1.0"), VersionUtility.ParseFrameworkName("net45"));

            AssertDependencies(testProvider.Dependencies, that => that
                .Needs("a", "1.0")
                .Needs("b", "1.0")
                .Needs("c", "1.0")
                .Needs("d", "1.0"));
        }


        [Fact]
        public void MissingDependenciesAreIgnored()
        {
            var testProvider = new TestDependencyProvider()
                .Package("a", "1.0", that => that.Needs("x", "1.0"));

            var walker = new DependencyWalker(new[] { testProvider });
            walker.Walk("a", new SemanticVersion("1.0"), VersionUtility.ParseFrameworkName("net45"));

            AssertDependencies(testProvider.Dependencies, that => that
                .Needs("a", "1.0"));
        }

        [Fact]
        public void RecursiveDependenciesAreNotFollowed()
        {
            var testProvider = new TestDependencyProvider()
                .Package("a", "1.0", that => that.Needs("b", "1.0"))
                .Package("b", "1.0", that => that.Needs("c", "1.0"))
                .Package("c", "1.0", that => that.Needs("d", "1.0").Needs("b", "1.0"))
                .Package("d", "1.0", that => that.Needs("b", "1.0"));

            var walker = new DependencyWalker(new[] { testProvider });

            Assert.Throws<InvalidOperationException>(() =>
            {
                walker.Walk("a", new SemanticVersion("1.0"), VersionUtility.ParseFrameworkName("net45"));
            });
        }

        [Fact]
        public void NearestDependencyVersionWins()
        {
            var testProvider = new TestDependencyProvider()
                .Package("a", "1.0", that => that.Needs("b", "1.0").Needs("c", "1.0").Needs("x", "1.0"))
                .Package("b", "1.0", that => that.Needs("x", "2.0"))
                .Package("c", "1.0", that => that.Needs("x", "2.0"))
                .Package("x", "1.0")
                .Package("x", "2.0");

            var walker = new DependencyWalker(new[] { testProvider });
            walker.Walk("a", new SemanticVersion("1.0"), VersionUtility.ParseFrameworkName("net45"));

            AssertDependencies(testProvider.Dependencies, that => that
                .Needs("a", "1.0")
                .Needs("b", "1.0")
                .Needs("c", "1.0")
                .Needs("x", "1.0"));
        }


        [Fact]
        public void HigherDisputedDependencyWins()
        {
            var testProvider = new TestDependencyProvider()
                .Package("a", "1.0", that => that.Needs("b", "1.0").Needs("c", "1.0"))
                .Package("b", "1.0", that => that.Needs("x", "1.0"))
                .Package("c", "1.0", that => that.Needs("x", "2.0"))
                .Package("x", "1.0")
                .Package("x", "2.0");

            var walker = new DependencyWalker(new[] { testProvider });
            walker.Walk("a", new SemanticVersion("1.0"), VersionUtility.ParseFrameworkName("net45"));

            AssertDependencies(testProvider.Dependencies, that => that
                .Needs("a", "1.0")
                .Needs("b", "1.0")
                .Needs("c", "1.0")
                .Needs("x", "2.0"));
        }

        [Fact]
        public void RejectedDependenciesToNotCarryConstraints()
        {
            // a1->b1-*d1->e2->x2
            // a1->c1->d2
            // a1->c1->e1->x1
            // * b1->d1 lower than c1->d2 so d1->e2->x2 are n/a

            var testProvider = new TestDependencyProvider()
                .Package("a", "1.0", that => that.Needs("b", "1.0").Needs("c", "1.0"))
                .Package("b", "1.0", that => that.Needs("d", "1.0"))
                .Package("c", "1.0", that => that.Needs("d", "2.0").Needs("e", "1.0"))
                .Package("d", "1.0", that => that.Needs("e", "2.0"))
                .Package("d", "2.0")
                .Package("e", "1.0", that => that.Needs("x", "1.0"))
                .Package("e", "2.0", that => that.Needs("x", "2.0"))
                .Package("x", "1.0")
                .Package("x", "2.0")
                .Package("g", version: null);

            var walker = new DependencyWalker(new[] { testProvider });
            walker.Walk("a", new SemanticVersion("1.0"), VersionUtility.ParseFrameworkName("net45"));

            // the d1->e2->x2 line has no effect because d2 has no dependencies, 

            AssertDependencies(testProvider.Dependencies, that => that
                .Needs("a", "1.0")
                .Needs("b", "1.0")
                .Needs("c", "1.0")
                .Needs("d", "2.0")
                .Needs("e", "1.0")
                .Needs("x", "1.0"));
        }
    }

    public class TestDependencyProvider : IDependencyProvider
    {
        private readonly IDictionary<LibraryRange, Entry> _entries = new Dictionary<LibraryRange, Entry>();
        public IEnumerable<LibraryDependency> Dependencies { get; set; }
        public FrameworkName FrameworkName { get; set; }

        public IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            return null;
        }

        public LibraryDescription GetDescription(LibraryRange libraryRange, FrameworkName frameworkName)
        {
            Logger.TraceInformation("StubAssemblyLoader.GetDependencies {0} {1} {2}", libraryRange.Name, libraryRange.VersionRange, frameworkName);
            Entry entry;
            if (!_entries.TryGetValue(libraryRange, out entry))
            {
                return null;
            }

            var d = entry.Dependencies as LibraryDependency[] ?? entry.Dependencies.ToArray();
            Logger.TraceInformation("StubAssemblyLoader.GetDependencies {0} {1}", d.Aggregate("", (a, b) => a + " " + b), frameworkName);

            return new LibraryDescription
            {
                Identity = new Library { Name = libraryRange.Name, Version = libraryRange.VersionRange.MinVersion },
                Dependencies = entry.Dependencies
            };
        }

        public void Initialize(IEnumerable<LibraryDescription> packages, FrameworkName frameworkName, string runtimeIdentifier)
        {
            var d = packages.Select(CreateDependency).ToArray();

            Logger.TraceInformation("StubAssemblyLoader.Initialize {0} {1} {2}", d.Aggregate("", (a, b) => a + " " + b), frameworkName, runtimeIdentifier);

            Dependencies = d;
            FrameworkName = frameworkName;
        }

        public IEnumerable<ICompilationMessage> GetDiagnostics()
        {
            return Enumerable.Empty<ICompilationMessage>();
        }

        private static LibraryDependency CreateDependency(LibraryDescription libraryDescription)
        {
            return new LibraryDependency
            {
                LibraryRange = new LibraryRange
                {
                    Name = libraryDescription.Identity.Name,
                    IsGacOrFrameworkReference = libraryDescription.Identity.IsGacOrFrameworkReference,
                    VersionRange = new SemanticVersionRange(libraryDescription.Identity.Version)
                }
            };
        }

        public TestDependencyProvider Package(string name, string version)
        {
            return Package(name, version, _ => { });
        }

        public TestDependencyProvider Package(string name, string version, Action<Entry> configure)
        {
            var entry = new Entry
            {
                Key = new Library
                {
                    Name = name,
                    Version = version == null ? null : new SemanticVersion(version)
                }
            };

            _entries[entry.Key] = entry;
            configure(entry);
            return this;
        }

        public class Entry
        {
            public Entry()
            {
                Dependencies = new List<LibraryDependency>();
            }

            public Library Key { get; set; }
            public IList<LibraryDependency> Dependencies { get; private set; }

            public Entry Needs(string name, string version)
            {
                Dependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = name,
                        VersionRange = new SemanticVersionRange(new SemanticVersion(version))
                    }
                });

                return this;
            }
        }

    }
}
