using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public class FrameworkExpander
    {
        private readonly IFrameworkNameProvider _mappings;

        public FrameworkExpander()
            : this(DefaultFrameworkNameProvider.Instance)
        {

        }

        public FrameworkExpander(IFrameworkNameProvider mappings)
        {
            _mappings = mappings;
        }

        /// <summary>
        /// Return all possible equivalent, subset, and known compatible frameworks.
        /// </summary>
        public IEnumerable<NuGetFramework> Expand(NuGetFramework framework)
        {
            HashSet<NuGetFramework> seen = new HashSet<NuGetFramework>(NuGetFramework.Comparer) { framework };
            Stack<NuGetFramework> toExpand = new Stack<NuGetFramework>();
            toExpand.Push(framework);

            while (toExpand.Count > 0)
            {
                foreach (var expansion in ExpandInternal(toExpand.Pop()))
                {
                    // only return distinct frameworks
                    if (seen.Add(expansion))
                    {
                        yield return expansion;

                        toExpand.Push(expansion);
                    }
                }
            }

            yield break;
        }

        /// <summary>
        /// Finds all expansions using the mapping provider
        /// </summary>
        private IEnumerable<NuGetFramework> ExpandInternal(NuGetFramework framework)
        {
            // check the framework directly, this includes profiles which the range doesn't return
            IEnumerable<NuGetFramework> directlyEquivalent = null;
            if (_mappings.TryGetEquivalentFrameworks(framework, out directlyEquivalent))
            {
                foreach (var eqFw in directlyEquivalent)
                {
                    yield return eqFw;
                }
            }

            // 0.0 through the current framework
            FrameworkRange frameworkRange = new FrameworkRange(
                new NuGetFramework(framework.Framework, new Version(0, 0), framework.Profile, framework.Platform, framework.PlatformVersion),
                framework);

            IEnumerable<NuGetFramework> equivalent = null;
            if (_mappings.TryGetEquivalentFrameworks(frameworkRange, out equivalent))
            {
                foreach (var eqFw in equivalent)
                {
                    yield return eqFw;
                }
            }

            // find all possible sub set frameworks if no profile is used
            if (!framework.HasProfile)
            {
                IEnumerable<string> subSetFrameworks = null;
                if (_mappings.TryGetSubSetFrameworks(framework.Framework, out subSetFrameworks))
                {
                    foreach (var subFramework in subSetFrameworks)
                    {
                        // clone the framework but use the sub framework instead
                        yield return new NuGetFramework(subFramework, framework.Version, framework.Profile, framework.Platform, framework.PlatformVersion);
                    }
                }
            }

            // explicit compatiblity mappings
            IEnumerable<FrameworkRange> ranges = null;
            if (_mappings.TryGetCompatibilityMappings(framework, out ranges))
            {
                foreach (var range in ranges)
                {
                    yield return range.Min;

                    if (!range.Min.Equals(range.Max))
                    {
                        yield return range.Max;
                    }
                }
            }

            yield break;
        }
    }
}
