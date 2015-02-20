using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    /// <summary>
    /// Reduces a list of frameworks into the smallest set of frameworks required.
    /// </summary>
    public class FrameworkReducer
    {
        private readonly IFrameworkNameProvider _mappings;
        private readonly IFrameworkCompatibilityProvider _compat;
        private readonly NuGetFrameworkFullComparer _fullComparer;
        private readonly NuGetFrameworkNameComparer _fwNameComparer;

        /// <summary>
        /// Creates a FrameworkReducer using the default framework mappings.
        /// </summary>
        public FrameworkReducer()
            : this(DefaultFrameworkNameProvider.Instance, DefaultCompatibilityProvider.Instance)
        {

        }

        /// <summary>
        /// Creates a FrameworkReducer using custom framework mappings.
        /// </summary>
        public FrameworkReducer(IFrameworkNameProvider mappings, IFrameworkCompatibilityProvider compat)
        {
            _mappings = mappings;
            _compat = compat;
            _fullComparer = new NuGetFrameworkFullComparer();
            _fwNameComparer = new NuGetFrameworkNameComparer();
        }

        /// <summary>
        /// Returns the nearest matching framework that is compatible.
        /// </summary>
        /// <param name="framework">Project target framework</param>
        /// <param name="possibleFrameworks">Possible frameworks to narrow down</param>
        /// <returns>Nearest compatible framework. If no frameworks are compatible null is returned.</returns>
        public NuGetFramework GetNearest(NuGetFramework framework, IEnumerable<NuGetFramework> possibleFrameworks)
        {
            NuGetFramework nearest = null;

            // Unsupported frameworks always lose, throw them out unless it's all we were given
            if (possibleFrameworks.Any(e => e != NuGetFramework.UnsupportedFramework))
            {
                possibleFrameworks = possibleFrameworks.Where(e => e != NuGetFramework.UnsupportedFramework);
            }

            // Try exact matches first
            nearest = possibleFrameworks.Where(f => _fullComparer.Equals(framework, f)).FirstOrDefault();

            if (nearest == null)
            {
                // Elimate non-compatible frameworks
                IEnumerable<NuGetFramework> compatible = possibleFrameworks.Where(f => _compat.IsCompatible(framework, f));

                // Remove lower versions of compatible frameworks
                IEnumerable<NuGetFramework> reduced = ReduceUpwards(compatible);

                // Reduce to the same framework name if possible
                if (reduced.Count() > 1 && reduced.Any(f => _fwNameComparer.Equals(f, framework)))
                {
                    reduced = reduced.Where(f => _fwNameComparer.Equals(f, framework));
                }

                // PCL reduce
                if (reduced.Count() > 1)
                {
                    // if we have a pcl and non-pcl mix, throw out the pcls
                    if (reduced.Any(f => f.IsPCL) && reduced.Any(f => !f.IsPCL))
                    {
                        reduced = reduced.Where(f => !f.IsPCL);
                    }
                    else if (reduced.All(f => f.IsPCL))
                    {
                        // decide between PCLs
                        // TODO: improve this

                        // For now just find the compatible PCL with the fewest frameworks
                        reduced = reduced.OrderBy(e => e.Profile.Split('+').Length).ThenBy(e => e.Profile.Length);
                    }
                }

                // Profile reduce
                if (reduced.Count() > 1 && !reduced.Any(f => f.IsPCL))
                {
                    // Prefer frameworks without profiles
                    // TODO: should we try to match against the profile of the input framework?
                    if (reduced.Any(f => f.HasProfile) && reduced.Any(f => !f.HasProfile))
                    {
                        reduced = reduced.Where(f => !f.HasProfile);
                    }
                }

                Debug.Assert(reduced.Count() < 2, "Unable to find the nearest framework: " + String.Join(", ", reduced));

                // if we have reduced down to a single framework, use that
                nearest = reduced.SingleOrDefault();

                // this should be a very rare occurrence
                // at this point we are unable to decide between the remaining frameworks in any useful way
                // just take the first one by rev alphabetical order if we can't narrow it down at all
                if (nearest != null && reduced.Any())
                {
                    nearest = reduced.OrderByDescending(f => f.Framework, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.GetHashCode()).First();
                }
            }

            return nearest;
        }

        /// <summary>
        /// Remove duplicates found in the equivalence mappings.
        /// </summary>
        public IEnumerable<NuGetFramework> Reduce(IEnumerable<NuGetFramework> frameworks)
        {
            // order first so we get consistent results for equivalent frameworks
            NuGetFramework[] input = frameworks.OrderBy(f => f.DotNetFrameworkName, StringComparer.OrdinalIgnoreCase).ToArray();

            var comparer = new NuGetFrameworkFullComparer();

            for (int i = 0; i < input.Length; i++)
            {
                bool dupe = false;

                IEnumerable<NuGetFramework> eqFrameworks = null;
                if (!_mappings.TryGetEquivalentFrameworks(input[i], out eqFrameworks))
                {
                    eqFrameworks = new List<NuGetFramework>() { input[i] };
                }

                for (int j = i + 1; !dupe && j < input.Length; j++)
                {
                    dupe = eqFrameworks.Contains(input[j], comparer);
                }

                if (!dupe)
                {
                    yield return input[i];
                }
            }

            yield break;
        }

        /// <summary>
        /// Reduce to the highest framework
        /// Ex: net45, net403, net40 -> net45
        /// </summary>
        public IEnumerable<NuGetFramework> ReduceUpwards(IEnumerable<NuGetFramework> frameworks)
        {
            // NuGetFramework.AnyFramework is a special case
            if (frameworks.Any(e => e != NuGetFramework.AnyFramework))
            {
                // Remove all instances of Any unless it is the only one in the list
                frameworks = frameworks.Where(e => e != NuGetFramework.AnyFramework);
            }

            // x: net40 j: net45 -> remove net40
            // x: wp8 j: win8 -> keep wp8
            return ReduceCore(frameworks, (x, y) => _compat.IsCompatible(y, x)).ToArray();
        }

        /// <summary>
        /// Reduce to the lowest framework
        /// Ex: net45, net403, net40 -> net40
        /// </summary>
        public IEnumerable<NuGetFramework> ReduceDownwards(IEnumerable<NuGetFramework> frameworks)
        {
            // NuGetFramework.AnyFramework is a special case
            if (frameworks.Any(e => e == NuGetFramework.AnyFramework))
            {
                // Any is always the lowest
                return new NuGetFramework[] { NuGetFramework.AnyFramework };
            }

            return ReduceCore(frameworks, (x, y) => _compat.IsCompatible(x, y)).ToArray();
        }

        private IEnumerable<NuGetFramework> ReduceCore(IEnumerable<NuGetFramework> frameworks, Func<NuGetFramework, NuGetFramework, bool> isCompat)
        {
            // remove duplicate frameworks
            NuGetFramework[] input = frameworks.Distinct(_fullComparer).ToArray();

            List<NuGetFramework> results = new List<NuGetFramework>(input.Length);

            for (int i = 0; i < input.Length; i++)
            {
                bool dupe = false;

                NuGetFramework x = input[i];

                for (int j = 0; !dupe && j < input.Length; j++)
                {
                    if (j != i)
                    {
                        NuGetFramework y = input[j];

                        // remove frameworks that are compatible with other framworks in the list
                        // do not remove frameworks which tie with others, for example: net40 and net40-client
                        // these equivalent frameworks should both be returned to let the caller decide between them
                        dupe = isCompat(x, y) && !isCompat(y, x);
                    }
                }

                if (!dupe)
                {
                    results.Add(input[i]);
                }
            }

            // sort the results just to make this more deterministic for the callers
            return results.OrderBy(f => f.Framework, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.ToString());
        }
    }
}
