using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Resolver
{
    /// <summary>
    /// A core package dependency resolver.
    /// </summary>
    /// <remarks>Not thread safe (yet)</remarks>
    public class PackageResolver : IPackageResolver
    {
        private DependencyBehavior _dependencyBehavior;
        private HashSet<PackageIdentity> _installedPackages;
        private HashSet<string> _newPackageIds;

        /// <summary>
        /// Core package resolver
        /// </summary>
        /// <param name="dependencyBehavior">Dependency version behavior</param>
        public PackageResolver(DependencyBehavior dependencyBehavior)
        {
            _dependencyBehavior = dependencyBehavior;
        }

        public IEnumerable<PackageIdentity> Resolve(IEnumerable<PackageIdentity> targets, IEnumerable<PackageDependencyInfo> availablePackages, CancellationToken token)
        {
            return Resolve(targets, availablePackages, Enumerable.Empty<PackageReference>(), token);
        }

        public IEnumerable<PackageIdentity> Resolve(IEnumerable<string> targets, IEnumerable<PackageDependencyInfo> availablePackages, CancellationToken token)
        {
            return Resolve(targets, availablePackages, Enumerable.Empty<PackageReference>(), token);
        }

        public IEnumerable<PackageIdentity> Resolve(IEnumerable<string> targets, IEnumerable<PackageDependencyInfo> availablePackages, IEnumerable<PackageReference> installedPackages, CancellationToken token)
        {
            return Resolve(targets.Select(id => new PackageIdentity(id, null)), availablePackages, installedPackages, token);
        }

        public IEnumerable<PackageIdentity> Resolve(IEnumerable<PackageIdentity> targets, IEnumerable<PackageDependencyInfo> availablePackages, IEnumerable<PackageReference> installedPackages, CancellationToken token)
        {
            if (installedPackages != null)
            {
                _installedPackages = new HashSet<PackageIdentity>(installedPackages.Select(e => e.PackageIdentity), PackageIdentity.Comparer);
            }

            // find the list of new packages to add
            _newPackageIds = new HashSet<string>(targets.Select(e => e.Id).Except(_installedPackages.Select(e => e.Id), StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);


            // validation 
            foreach (var target in targets)
            {
                if (!availablePackages.Any(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, target.Id)))
                {
                    throw new NuGetResolverInputException(String.Format(CultureInfo.CurrentUICulture, Strings.MissingDependencyInfo, target.Id));
                }
            }

            // validation 
            foreach (var installed in _installedPackages)
            {
                if (!availablePackages.Any(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, installed.Id)))
                {
                    throw new NuGetResolverInputException(String.Format(CultureInfo.CurrentUICulture, Strings.MissingDependencyInfo, installed.Id));
                }
            }

            // TODO: this will be removed later when the interface changes
            foreach (var installed in _installedPackages)
            {
                if (!targets.Any(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, installed.Id)))
                {
                    throw new NuGetResolverInputException("Installed packages should be passed as targets");
                }
            }

            // Solve
            var solver = new CombinationSolver<ResolverPackage>();

            CompareWrapper<ResolverPackage> comparer = new CompareWrapper<ResolverPackage>(Compare);

            List<List<ResolverPackage>> grouped = new List<List<ResolverPackage>>();

            var packageComparer = PackageIdentity.Comparer;

            List<ResolverPackage> resolverPackages = new List<ResolverPackage>();

            // convert the available packages into ResolverPackages
            foreach (var package in availablePackages)
            {
                IEnumerable<PackageDependency> dependencies = null;

                // clear out the dependencies if the behavior is set to ignore
                if (_dependencyBehavior == DependencyBehavior.Ignore)
                {
                    dependencies = Enumerable.Empty<PackageDependency>();
                }
                else
                {
                    dependencies = package.Dependencies;
                }

                resolverPackages.Add(new ResolverPackage(package.Id, package.Version, dependencies));
            }

            // group the packages by id
            foreach (var group in resolverPackages.GroupBy(e => e.Id))
            {
                List<ResolverPackage> curSet = group.ToList();

                // add an absent package for non-targets
                // being absent allows the resolver to throw it out if it is not needed
                if (!targets.Any(e => StringComparer.OrdinalIgnoreCase.Equals(e.Id, group.Key)))
                {
                    curSet.Add(new ResolverPackage(group.Key, null, null, true));
                }

                grouped.Add(curSet);
            }

            var solution = solver.FindSolution(grouped, comparer, ShouldRejectPackagePair);

            var nonAbsentCandidates = solution.Where(c => !c.Absent);

            if (nonAbsentCandidates.Any())
            {
                var sortedSolution = TopologicalSort(nonAbsentCandidates);

                return sortedSolution.ToArray();
            }

            // no solution found
            throw new NuGetResolverConstraintException(Strings.NoSolution);
        }

        private IEnumerable<ResolverPackage> TopologicalSort(IEnumerable<ResolverPackage> nodes)
        {
            List<ResolverPackage> result = new List<ResolverPackage>();

            var dependsOn = new Func<ResolverPackage, ResolverPackage, bool>((x, y) =>
            {
                return x.FindDependencyRange(y.Id) != null;
            });

            var dependenciesAreSatisfied = new Func<ResolverPackage, bool>(node =>
            {
                var dependencies = node.Dependencies;
                return dependencies == null || !dependencies.Any() ||
                       dependencies.All(d => result.Any(r => StringComparer.OrdinalIgnoreCase.Equals(r.Id, d.Id)));
            });

            var satisfiedNodes = new HashSet<ResolverPackage>(nodes.Where(n => dependenciesAreSatisfied(n)));
            while (satisfiedNodes.Any())
            {
                //Pick any element from the set. Remove it, and add it to the result list.
                var node = satisfiedNodes.First();
                satisfiedNodes.Remove(node);
                result.Add(node);

                // Find unprocessed nodes that depended on the node we just added to the result.
                // If all of its dependencies are now satisfied, add it to the set of nodes to process.
                var newlySatisfiedNodes = nodes.Except(result)
                                               .Where(n => dependsOn(n, node))
                                               .Where(n => dependenciesAreSatisfied(n));

                foreach (var cur in newlySatisfiedNodes)
                {
                    satisfiedNodes.Add(cur);
                }
            }

            return result;
        }

        private int Compare(ResolverPackage x, ResolverPackage y)
        {
            Debug.Assert(string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase));

            // The absent package comes first in the sort order
            bool isXAbsent = x.Absent;
            bool isYAbsent = y.Absent;
            if (isXAbsent && !isYAbsent)
            {
                return -1;
            }
            if (!isXAbsent && isYAbsent)
            {
                return 1;
            }
            if (isXAbsent && isYAbsent)
            {
                return 0;
            }

            if (_installedPackages != null)
            {
                //Already installed packages come next in the sort order.
                bool xInstalled = _installedPackages.Contains(x);
                bool yInstalled = _installedPackages.Contains(y);
                if (xInstalled && !yInstalled)
                {
                    return -1;
                }

                if (!xInstalled && yInstalled)
                {
                    return 1;
                }
            }

            var xv = x.Version;
            var yv = y.Version;

            DependencyBehavior packageBehavior = _dependencyBehavior;

            // for new packages use the highest version
            if (_newPackageIds.Contains(x.Id))
            {
                packageBehavior = DependencyBehavior.Highest;
            }

            switch (_dependencyBehavior)
            {
                case DependencyBehavior.Lowest:
                    return VersionComparer.Default.Compare(xv, yv);
                case DependencyBehavior.Ignore:
                case DependencyBehavior.Highest:
                    return -1 * VersionComparer.Default.Compare(xv, yv);
                case DependencyBehavior.HighestMinor:
                    {
                        if (VersionComparer.Default.Equals(xv, yv)) return 0;

                        //TODO: This is surely wrong...
                        return new[] { x, y }.OrderBy(p => p.Version.Major)
                                           .ThenByDescending(p => p.Version.Minor)
                                           .ThenByDescending(p => p.Version.Patch).FirstOrDefault() == x ? -1 : 1;

                    }
                case DependencyBehavior.HighestPatch:
                    {
                        if (VersionComparer.Default.Equals(xv, yv)) return 0;

                        //TODO: This is surely wrong...
                        return new[] { x, y }.OrderBy(p => p.Version.Major)
                                             .ThenBy(p => p.Version.Minor)
                                             .ThenByDescending(p => p.Version.Patch).FirstOrDefault() == x ? -1 : 1;
                    }
                default:
                    throw new InvalidOperationException("Unknown DependencyBehavior value.");
            }
        }

        private static bool ShouldRejectPackagePair(ResolverPackage p1, ResolverPackage p2)
        {
            var p1ToP2Dependency = p1.FindDependencyRange(p2.Id);
            if (p1ToP2Dependency != null)
            {
                return p2.Absent || !p1ToP2Dependency.Satisfies(p2.Version);
            }

            var p2ToP1Dependency = p2.FindDependencyRange(p1.Id);
            if (p2ToP1Dependency != null)
            {
                return p1.Absent || !p2ToP1Dependency.Satisfies(p1.Version);
            }

            return false;
        }
    }
}
