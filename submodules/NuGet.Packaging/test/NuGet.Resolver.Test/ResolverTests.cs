using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using NuGet.Resolver;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.Packaging;
using System.Threading;

namespace NuGet.Resolver.Test
{
    public class ResolverTests
    {
        [Fact]
        public void Resolver_IgnoreDependencies()
        {
            var target = CreatePackage("A", "1.0", new Dictionary<string, string>() { { "B", null }, { "C", null } });

            var sourceRepository = new List<ResolverPackage>() { 
                target, 
                CreatePackage("B", "1.0", new Dictionary<string, string>() { { "D", null } }),
                CreatePackage("C", "1.0", new Dictionary<string, string>() { { "D", null } }),
                CreatePackage("D", "1.0"),
            };

            var resolver = new PackageResolver(DependencyBehavior.Ignore);
            var packages = resolver.Resolve(new ResolverPackage[] { target }, sourceRepository, CancellationToken.None).ToDictionary(p => p.Id);

            // Assert
            Assert.Equal(1, packages.Count());
            Assert.NotNull(packages["A"]);
        }

        [Fact]
        public void ResolveDependenciesForInstallDiamondDependencyGraph()
        {
            // Arrange
            // A -> [B, C]
            // B -> [D]
            // C -> [D]
            //    A
            //   / \
            //  B   C
            //   \ /
            //    D 
            var target = CreatePackage("A", "1.0", new Dictionary<string, string>() { { "B", null }, { "C", null } });

            var sourceRepository = new List<ResolverPackage>() { 
                target, 
                CreatePackage("B", "1.0", new Dictionary<string, string>() { { "D", null } }),
                CreatePackage("C", "1.0", new Dictionary<string, string>() { { "D", null } }),
                CreatePackage("D", "1.0"),
            };

            var resolver = new PackageResolver(DependencyBehavior.Lowest);
            var packages = resolver.Resolve(new ResolverPackage[] { target }, sourceRepository, CancellationToken.None).ToDictionary(p => p.Id);

            // Assert
            Assert.Equal(4, packages.Count());
            Assert.NotNull(packages["A"]);
            Assert.NotNull(packages["B"]);
            Assert.NotNull(packages["C"]);
            Assert.NotNull(packages["D"]);
        }

        [Fact]
        public void ResolveDependenciesForInstallDiamondDependencyGraphWithDifferentVersionsOfSamePackage()
        {
            // Arrange
            var sourceRepository = new List<ResolverPackage>();
            // A -> [B, C]
            // B -> [D >= 1, E >= 2]
            // C -> [D >= 2, E >= 1]
            //     A
            //   /   \
            //  B     C
            //  | \   | \
            //  D1 E2 D2 E1

            var packageA = CreatePackage("A", "1.0", new Dictionary<string, string>() { { "B", null }, { "C", null } });
            var packageB = CreatePackage("B", "1.0", new Dictionary<string, string>() { { "D", "1.0" }, { "E", "2.0" } });
            var packageC = CreatePackage("C", "1.0", new Dictionary<string, string>() { { "D", "2.0" }, { "E", "1.0" } });
            var packageD1 = CreatePackage("D", "1.0");
            var packageD2 = CreatePackage("D", "2.0");
            var packageE1 = CreatePackage("E", "1.0");
            var packageE2 = CreatePackage("E", "2.0");

            sourceRepository.Add(packageA);
            sourceRepository.Add(packageB);
            sourceRepository.Add(packageC);
            sourceRepository.Add(packageD2);
            sourceRepository.Add(packageD1);
            sourceRepository.Add(packageE2);
            sourceRepository.Add(packageE1);

            // Act
            var resolver = new PackageResolver(DependencyBehavior.Lowest);
            var solution = resolver.Resolve(new ResolverPackage[] { packageA }, sourceRepository, CancellationToken.None).ToArray();
            var packages = solution.ToDictionary(p => p.Id);

            // Assert
            Assert.Equal(5, packages.Count());

            Assert.Equal("1.0.0", packages["A"].Version.ToNormalizedString());
            Assert.Equal("1.0.0", packages["B"].Version.ToNormalizedString());
            Assert.Equal("1.0.0", packages["C"].Version.ToNormalizedString());
            Assert.Equal("2.0.0", packages["D"].Version.ToNormalizedString());
            Assert.Equal("2.0.0", packages["E"].Version.ToNormalizedString());

            //Verify that D & E are first (order doesn't matter), then B & C (order doesn't matter), then A
            Assert.True(solution.Take(2).Select(a => a.Id).All(id => id == "D" || id == "E"));
            Assert.True(solution.Skip(2).Take(2).Select(a => a.Id).All(id => id == "B" || id == "C"));
            Assert.Equal("A", solution[4].Id);
        }

        // Tests that when there is a local package that can satisfy all dependencies, it is preferred over other packages.
        [Fact]
        public void ResolveActionsPreferInstalledPackages()
        {
            // Arrange

            // Local:
            // B 1.0
            // C 1.0

            // Remote
            // A 1.0 -> B 1.0, C 1.0
            // B 1.0
            // B 1.1
            // C 1.0
            // C 2.0
            var target = CreatePackage("A", "1.0", new Dictionary<string, string>() { { "B", "1.0" }, { "C", "1.0" } });

            // Expect: Install A 1.0 (no change to B or C)
            var sourceRepository = new List<ResolverPackage>() {
                target, 
                CreatePackage("B", "1.0"),
                CreatePackage("B", "1.1"),
                CreatePackage("C", "1.0"),
                CreatePackage("C", "2.0"),
            };

            var install = new List<PackageReference>() {
                new PackageReference(new PackageIdentity("B", NuGetVersion.Parse("1.0")), null),
                new PackageReference(new PackageIdentity("C", NuGetVersion.Parse("1.0")), null),
            };

            List<PackageIdentity> targets = new List<PackageIdentity>();
            targets.Add(target);
            targets.AddRange(install.Select(e => e.PackageIdentity));

            // Act
            var resolver = new PackageResolver(DependencyBehavior.HighestMinor);
            var solution = resolver.Resolve(targets, sourceRepository, install, CancellationToken.None).ToArray();
            var packages = solution.ToDictionary(p => p.Id);

            // Assert
            Assert.Equal(3, packages.Count);
            Assert.Equal("1.0.0", packages["A"].Version.ToNormalizedString());
            Assert.Equal("1.0.0", packages["B"].Version.ToNormalizedString());
            Assert.Equal("1.0.0", packages["C"].Version.ToNormalizedString());
        }

        [Fact]
        public void ResolveActionsForSimpleUpdate()
        {
            // Arrange
            // Installed: A, B
            // A 1.0 -> B [1.0]
            var project = new List<ResolverPackage>()
            {
                CreatePackage("A", "1.0", new Dictionary<string, string> { { "B", "1.0" } } ),
                CreatePackage("B", "1.0"),
            };

            var target = CreatePackage("A", "2.0", new Dictionary<string, string> { { "B", "1.0" } });

            var sourceRepository = new List<ResolverPackage>()
            {
                target,
                CreatePackage("B", "1.0"),
            };

            // Act
            var resolver = new PackageResolver(DependencyBehavior.HighestPatch);
            var solution = resolver.Resolve(new ResolverPackage[] { target }, sourceRepository, CancellationToken.None).ToArray();
            var packages = solution.ToDictionary(p => p.Id);

            // Assert
            Assert.Equal(2, solution.Length);
            Assert.Equal("2.0.0", packages["A"].Version.ToNormalizedString());

        }

        [Fact]
        public void ResolvesLowestMajorHighestMinorHighestPatchVersionOfListedPackagesForDependencies()
        {
            // Arrange

            // A 1.0 -> B 1.0
            // B 1.0 -> C 1.1
            // C 1.1 -> D 1.0

            var target = CreatePackage("A", "1.0", new Dictionary<string, string>() { { "B", "1.0" } });

            var sourceRepository = new List<ResolverPackage>() {
                target,
                CreatePackage("B", "2.0", new Dictionary<string, string>() { { "C", "1.1" } }),
                CreatePackage("B", "1.0", new Dictionary<string, string>() { { "C", "1.1" } }),
                CreatePackage("B", "1.0.1"),
                CreatePackage("D", "2.0"),
                CreatePackage("C", "1.1.3", new Dictionary<string, string>() { { "D", "1.0" } }),
                CreatePackage("C", "1.1.1", new Dictionary<string, string>() { { "D", "1.0" } }),
                CreatePackage("C", "1.5.1", new Dictionary<string, string>() { { "D", "1.0" } }),
                CreatePackage("B", "1.0.9", new Dictionary<string, string>() { { "C", "1.1" } }),
                CreatePackage("B", "1.1", new Dictionary<string, string>() { { "C", "1.1" } })
            };

            // Act
            var resolver = new PackageResolver(DependencyBehavior.HighestMinor);

            var packages = resolver.Resolve(new ResolverPackage[] { target }, sourceRepository, CancellationToken.None).ToDictionary(p => p.Id);

            // Assert
            Assert.Equal(4, packages.Count);
            Assert.Equal("2.0.0", packages["D"].Version.ToNormalizedString());
            Assert.Equal("1.5.1", packages["C"].Version.ToNormalizedString());
            Assert.Equal("1.1.0", packages["B"].Version.ToNormalizedString());
            Assert.Equal("1.0.0", packages["A"].Version.ToNormalizedString());
        }


        // Tests that when DependencyVersion is Lowest, the dependency with the lowest major minor and highest patch version
        // is picked.
        [Fact]
        public void ResolvesLowestMajorAndMinorAndPatchVersionOfListedPackagesForDependencies()
        {
            // Arrange

            // A 1.0 -> B 1.0
            // B 1.0 -> C 1.1
            // C 1.1 -> D 1.0

            var target = CreatePackage("A", "1.0", new Dictionary<string, string>() { { "B", "1.0" } });

            var sourceRepository = new List<ResolverPackage>() {
                target,
                CreatePackage("B", "2.0", new Dictionary<string, string>() { { "C", "1.1" } }),
                CreatePackage("B", "1.0.1"),
                CreatePackage("D", "2.0"),
                CreatePackage("C", "1.1.3", new Dictionary<string, string>() { { "D", "1.0" } }),
                CreatePackage("C", "1.1.1", new Dictionary<string, string>() { { "D", "1.0" } }),
                CreatePackage("C", "1.5.1", new Dictionary<string, string>() { { "D", "1.0" } }),
                CreatePackage("B", "1.0.9", new Dictionary<string, string>() { { "C", "1.1" } }),
                CreatePackage("B", "1.1", new Dictionary<string, string>() { { "C", "1.1" } })
            };

            // Act
            var resolver = new PackageResolver(DependencyBehavior.Lowest);

            var packages = resolver.Resolve(new ResolverPackage[] { target }, sourceRepository, CancellationToken.None).ToDictionary(p => p.Id);

            // Assert
            Assert.Equal(2, packages.Count);
            Assert.Equal("1.0.1", packages["B"].Version.ToNormalizedString());
            Assert.Equal("1.0.0", packages["A"].Version.ToNormalizedString());
        }

        [Fact]
        public void ResolvesLowestMajorAndMinorHighestPatchVersionOfListedPackagesForDependencies()
        {
            // Arrange

            var target = CreatePackage("A", "1.0", new Dictionary<string, string>() { { "B", "1.0" } });

            // A 1.0 -> B 1.0
            // B 1.0 -> C 1.1
            // C 1.1 -> D 1.0
            var sourceRepository = new List<ResolverPackage>()
            {
                target,
                CreatePackage("B", "2.0", new Dictionary<string, string>() { { "C", "1.1" } }),
                CreatePackage("B", "1.0", new Dictionary<string, string>() { { "C", "1.1" } }),
                CreatePackage("B", "1.0.1"),
                CreatePackage("D", "2.0"),
                CreatePackage("C", "1.1.3", new Dictionary<string, string>() { { "D", "1.0" } }),
                CreatePackage("C", "1.1.1", new Dictionary<string, string>() { { "D", "1.0" } }),
                CreatePackage("C", "1.5.1", new Dictionary<string, string>() { { "D", "1.0" } }),
                CreatePackage("B", "1.0.9", new Dictionary<string, string>() { { "C", "1.1" } }),
                CreatePackage("B", "1.1", new Dictionary<string, string>() { { "C", "1.1" } })
            };

            // Act
            var resolver = new PackageResolver(DependencyBehavior.HighestPatch);
            var packages = resolver.Resolve(new ResolverPackage[] { target }, sourceRepository, CancellationToken.None).ToDictionary(p => p.Id);

            // Assert
            Assert.Equal(4, packages.Count);
            Assert.Equal("2.0.0", packages["D"].Version.ToNormalizedString());
            Assert.Equal("1.1.3", packages["C"].Version.ToNormalizedString());
            Assert.Equal("1.0.9", packages["B"].Version.ToNormalizedString());
            Assert.Equal("1.0.0", packages["A"].Version.ToNormalizedString());
        }

        private ResolverPackage CreatePackage(string id, string version, IDictionary<string, string> dependencies = null)
        {
            List<NuGet.Packaging.Core.PackageDependency> deps = new List<NuGet.Packaging.Core.PackageDependency>();

            if (dependencies != null)
            {
                foreach (var dep in dependencies)
                {
                    VersionRange range = null;

                    if (dep.Value != null)
                    {
                        range = VersionRange.Parse(dep.Value);
                    }

                    deps.Add(new NuGet.Packaging.Core.PackageDependency(dep.Key, range));
                }
            }

            return new ResolverPackage(id, NuGetVersion.Parse(version), deps);
        }

        [Fact]
        public void Resolver_Basic()
        {
            ResolverPackage target = new ResolverPackage("a", new NuGetVersion(1, 0, 0), 
                new NuGet.Packaging.Core.PackageDependency[] { 
                    new NuGet.Packaging.Core.PackageDependency("b", new VersionRange(new NuGetVersion(1, 0, 0), true, new NuGetVersion(3, 0, 0), true)) });

            var dep1 = new ResolverPackage("b", new NuGetVersion(2, 0, 0));
            var dep2 = new ResolverPackage("b", new NuGetVersion(2, 5, 0));
            var dep3 = new ResolverPackage("b", new NuGetVersion(4, 0, 0));

            List<ResolverPackage> possible = new List<ResolverPackage>();
            possible.Add(dep1);
            possible.Add(dep2);
            possible.Add(dep3);
            possible.Add(target);

            var resolver = new PackageResolver(DependencyBehavior.Lowest);
            var solution = resolver.Resolve(new ResolverPackage[] { target }, possible, CancellationToken.None).ToList();

            Assert.Equal(2, solution.Count());
        }

        [Fact]
        public void Resolver_NoSolution()
        {
            ResolverPackage target = new ResolverPackage("a", new NuGetVersion(1, 0, 0), new NuGet.Packaging.Core.PackageDependency[] { new NuGet.Packaging.Core.PackageDependency("b", null) });

            List<ResolverPackage> possible = new List<ResolverPackage>();
            possible.Add(target);

            var resolver = new PackageResolver(DependencyBehavior.Lowest);

            var solution = resolver.Resolve(new ResolverPackage[] { target }, possible, CancellationToken.None);

            Assert.Equal(0, solution.Count());
        }
    }
}
