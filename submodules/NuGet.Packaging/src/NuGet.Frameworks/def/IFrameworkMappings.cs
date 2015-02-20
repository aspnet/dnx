using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    /// <summary>
    /// A raw list of framework mappings. These are indexed by the framework name provider and in most cases all mappings are
    /// mirrored so that the IFrameworkMappings implementation only needs to provide the minimum amount of mappings.
    /// </summary>
    public interface IFrameworkMappings
    {
        /// <summary>
        /// Synonym -> Identifier
        /// Ex: NET Framework -> .NET Framework
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> IdentifierSynonyms { get; }

        /// <summary>
        /// Ex: .NET Framework -> net
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> IdentifierShortNames { get; }

        /// <summary>
        /// Ex: WindowsPhone -> wp
        /// </summary>;
        IEnumerable<FrameworkSpecificMapping> ProfileShortNames { get; }

        /// <summary>
        /// Equal frameworks. Used for legacy conversions.
        /// ex: Framework: Win8 <-> Framework: NetCore45 Platform: Win8
        /// </summary>
        IEnumerable<KeyValuePair<NuGetFramework, NuGetFramework>> EquivalentFrameworks { get; }

        /// <summary>
        /// Framework, EquivalentProfile1, EquivalentProfile2
        /// Ex: Silverlight, WindowsPhone71, WindowsPhone
        /// </summary>
        IEnumerable<FrameworkSpecificMapping> EquivalentProfiles { get; }

        /// <summary>
        /// Frameworks which are subsets of others.
        /// Ex: .NETCore -> .NET
        /// Everything in .NETCore maps to .NET and is one way compatible. Version numbers follow the same format.
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> SubSetFrameworks { get; }

        /// <summary>
        /// Additional framework compatibility rules beyond name and version matching.
        /// Ex: .NETFramework -supports-> Native
        /// </summary>
        IEnumerable<OneWayCompatibilityMappingEntry> CompatibilityMappings { get; }
    }
}
