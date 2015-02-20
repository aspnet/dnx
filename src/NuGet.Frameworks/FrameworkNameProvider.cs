using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public class FrameworkNameProvider : IFrameworkNameProvider
    {
        /// <summary>
        /// Contains identifier -> identifier
        /// Ex: .NET Framework -> .NET Framework
        /// Ex: NET Framework -> .NET Framework
        /// This includes self mappings.
        /// </summary>
        private Dictionary<string, string> _identifierSynonyms;
        private Dictionary<string, string> _identifierToShortName;
        private Dictionary<string, string> _profilesToShortName;
        private Dictionary<string, string> _identifierShortToLong;
        private Dictionary<string, string> _profileShortToLong;

        // profile -> supported frameworks, optional frameworks
        private Dictionary<int, HashSet<NuGetFramework>> _portableFrameworks;
        private Dictionary<int, HashSet<NuGetFramework>> _portableOptionalFrameworks;

        // equivalent frameworks
        private Dictionary<NuGetFramework, HashSet<NuGetFramework>> _equivalentFrameworks;

        // equivalent profiles
        private Dictionary<string, Dictionary<string, HashSet<string>>> _equivalentProfiles;

        // all compatibility mappings
        private HashSet<OneWayCompatibilityMappingEntry> _compatibilityMappings;

        // subsets, net -> netcore
        private Dictionary<string, HashSet<string>> _subSetFrameworks;

        public FrameworkNameProvider(IEnumerable<IFrameworkMappings> mappings, IEnumerable<IPortableFrameworkMappings> portableMappings)
        {
            _identifierSynonyms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _identifierToShortName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _profilesToShortName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _identifierShortToLong = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _profileShortToLong = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _portableFrameworks = new Dictionary<int, HashSet<NuGetFramework>>();
            _portableOptionalFrameworks = new Dictionary<int, HashSet<NuGetFramework>>();
            _equivalentFrameworks = new Dictionary<NuGetFramework, HashSet<NuGetFramework>>(NuGetFramework.Comparer);
            _equivalentProfiles = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
            _subSetFrameworks = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _compatibilityMappings = new HashSet<OneWayCompatibilityMappingEntry>(OneWayCompatibilityMappingEntry.Comparer);

            InitMappings(mappings);

            InitPortableMappings(portableMappings);
        }

        /// <summary>
        /// Converts a key using the mappings, or if the key is already converted, finds the normalized form.
        /// </summary>
        private static bool TryConvertOrNormalize(string key, IDictionary<string, string> mappings, IDictionary<string, string> reverse, out string value)
        {
            if (mappings.TryGetValue(key, out value))
            {
                return true;
            }
            else if (reverse.ContainsKey(key))
            {
                value = reverse.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Key, key)).Select(s => s.Key).Single();
                return true;
            }

            value = null;
            return false;
        }

        public bool TryGetIdentifier(string framework, out string identifier)
        {
            return TryConvertOrNormalize(framework, _identifierSynonyms, _identifierToShortName, out identifier);
        }

        public bool TryGetProfile(string frameworkIdentifier, string profileShortName, out string profile)
        {
            return TryConvertOrNormalize(profileShortName, _profileShortToLong, _profilesToShortName, out profile);
        }

        public bool TryGetShortIdentifier(string identifier, out string identifierShortName)
        {
            return TryConvertOrNormalize(identifier, _identifierToShortName, _identifierShortToLong, out identifierShortName);
        }

        public bool TryGetShortProfile(string frameworkIdentifier, string profile, out string profileShortName)
        {
            return TryConvertOrNormalize(profile, _profilesToShortName, _profileShortToLong, out profileShortName);
        }

        public bool TryGetVersion(string versionString, out Version version)
        {
            version = null;

            if (String.IsNullOrEmpty(versionString))
            {
                version = new Version(0, 0);
            }
            else
            {
                if (versionString.IndexOf('.') > -1)
                {
                    // parse the version as a normal dot delimited version
                    return Version.TryParse(versionString, out version);
                }
                else
                {
                    // make sure we have at least 2 digits
                    if (versionString.Length < 2)
                    {
                        versionString += "0";
                    }

                    // take only the first 4 digits and add dots
                    // 451 -> 4.5.1
                    // 81233 -> 8123
                    return Version.TryParse(String.Join(".", versionString.ToCharArray().Take(4)), out version);
                }
            }

            return false;
        }

        public string GetVersionString(Version version)
        {
            StringBuilder sb = new StringBuilder();

            if (version != null)
            {
                Stack<int> versionParts = new Stack<int>();

                versionParts.Push(version.Major > 0 ? version.Major : 0);
                versionParts.Push(version.Minor > 0 ? version.Minor : 0);
                versionParts.Push(version.Build > 0 ? version.Build : 0);
                versionParts.Push(version.Revision > 0 ? version.Revision : 0);

                // if any parts of the version are over 9 we need to use decimals
                bool useDecimals = versionParts.Any(x => x > 9);

                // remove all trailing zeros
                while (versionParts.Count > 0 && versionParts.Peek() <= 0)
                {
                    versionParts.Pop();
                }

                // write the version string out backwards
                while (versionParts.Count > 0)
                {
                    // avoid adding a decimal if this is the first digit, but if we are down 
                    // to only 2 numbers left we have to add a decimal otherwise 10.0 becomes 1.0
                    // during the parse
                    if (useDecimals)
                    {
                         if (sb.Length > 0)
                         {
                             sb.Insert(0, ".");
                         }
                         else if (versionParts.Count == 1)
                         {
                             sb.Append(".0");
                         }
                    }

                    sb.Insert(0, versionParts.Pop());
                }
            }

            return sb.ToString();
        }

        public bool TryGetPortableProfile(IEnumerable<NuGetFramework> supportedFrameworks, out int profileNumber)
        {
            if (supportedFrameworks == null)
            {
                throw new ArgumentNullException("supportedFrameworks");
            }

            profileNumber = -1;

            var frameworkComparer = new NuGetFrameworkNameComparer();
            var profileComparer = new NuGetFrameworkProfileComparer();
            HashSet<NuGetFramework> input = new HashSet<NuGetFramework>(supportedFrameworks, NuGetFramework.Comparer);

            foreach (var pair in _portableFrameworks)
            {
                // to match the required set must be less than or the same count as the input
                // if we knew which frameworks were optional in the input we could rule out the lesser ones also
                if (pair.Value.Count <= input.Count)
                {
                    List<NuGetFramework> reduced = new List<NuGetFramework>();
                    foreach (var curFw in supportedFrameworks)
                    {
                        bool isOptional = false;

                        foreach (var optional in GetOptionalFrameworks(pair.Key))
                        {
                            // TODO: profile check? Is the version check correct here?
                            if (frameworkComparer.Equals(optional, curFw) 
                                && profileComparer.Equals(optional, curFw)
                                && curFw.Version >= optional.Version)
                            {
                                isOptional = true;
                            }
                        }

                        if (!isOptional)
                        {
                            reduced.Add(curFw);
                        }
                    }

                    // check all frameworks while taking into account equivalent variations
                    var premutations = GetEquivalentPermutations(pair.Value).Select(p => new HashSet<NuGetFramework>(p, NuGetFramework.Comparer));
                    foreach (var permutation in premutations)
                    {
                        if (permutation.SetEquals(reduced))
                        {
                            // found a match
                            profileNumber = pair.Key;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        // find all combinations that are equivalent
        // ex: net4+win8 <-> net4+netcore45
        private IEnumerable<IEnumerable<NuGetFramework>> GetEquivalentPermutations(IEnumerable<NuGetFramework> frameworks)
        {
            if (frameworks.Any())
            {
                NuGetFramework current = frameworks.First();
                NuGetFramework[] remaining = frameworks.Skip(1).ToArray();

                // find all equivalent frameworks for the current one
                HashSet<NuGetFramework> equalFrameworks = null;
                if (!_equivalentFrameworks.TryGetValue(current, out equalFrameworks))
                {
                    equalFrameworks = new HashSet<NuGetFramework>(NuGetFramework.Comparer);
                }

                // include ourselves
                equalFrameworks.Add(current);

                foreach (var fw in equalFrameworks)
                {
                    var fwArray = new NuGetFramework[] { fw };

                    if (remaining.Length > 0)
                    {
                        foreach (var result in GetEquivalentPermutations(remaining))
                        {
                            // work backwards adding the frameworks into the sets
                            yield return result.Concat(fwArray);
                        }
                    }
                    else
                    {
                        yield return fwArray;
                    }
                }
            }

            yield break;
        }

        private IEnumerable<NuGetFramework> GetOptionalFrameworks(int profile)
        {
            HashSet<NuGetFramework> frameworks = null;

            if (_portableOptionalFrameworks.TryGetValue(profile, out frameworks))
            {
                return frameworks;
            }

            return Enumerable.Empty<NuGetFramework>();
        }

        public bool TryGetPortableFrameworks(int profile, out IEnumerable<NuGetFramework> frameworks)
        {
            return TryGetPortableFrameworks(profile, true, out frameworks);
        }

        public bool TryGetPortableFrameworks(int profile, bool includeOptional, out IEnumerable<NuGetFramework> frameworks)
        {
            List<NuGetFramework> result = new List<NuGetFramework>();
            HashSet<NuGetFramework> tmpFrameworks = null;
            if (_portableFrameworks.TryGetValue(profile, out tmpFrameworks))
            {
                foreach (var fw in tmpFrameworks)
                {
                    result.Add(fw);
                }
            }

            if (includeOptional)
            {
                HashSet<NuGetFramework> optional = null;
                if (_portableOptionalFrameworks.TryGetValue(profile, out optional))
                {
                    foreach (var fw in optional)
                    {
                        result.Add(fw);
                    }
                }
            }

            frameworks = result;
            return result.Count > 0;
        }

        public bool TryGetPortableFrameworks(string shortPortableProfiles, out IEnumerable<NuGetFramework> frameworks)
        {
            if (shortPortableProfiles == null)
            {
                throw new ArgumentNullException("shortPortableProfiles");
            }

            var shortNames = shortPortableProfiles.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);

            Debug.Assert(shortNames.Length > 0);

            List<NuGetFramework> result = new List<NuGetFramework>();
            foreach (var name in shortNames)
            {
                result.Add(NuGetFramework.Parse(name, this));
            }

            frameworks = result;
            return result.Count > 0;
        }

        public bool TryGetPortableFrameworks(string profile, bool includeOptional, out IEnumerable<NuGetFramework> frameworks)
        {
            // attempt to parse the profile for a number
            if (profile.StartsWith("Profile", StringComparison.OrdinalIgnoreCase))
            {
                string trimmed = profile.Substring(7, profile.Length - 7);

                int profileNum = -1;
                if (Int32.TryParse(trimmed, out profileNum) && TryGetPortableFrameworks(profileNum, includeOptional, out frameworks))
                {
                    return true;
                }
                else
                {
                    frameworks = Enumerable.Empty<NuGetFramework>();
                    return false;
                }
            }

            // treat the profile as a list of frameworks
            return TryGetPortableFrameworks(profile, out frameworks);
        }

        public bool TryGetEquivalentFrameworks(NuGetFramework framework, out IEnumerable<NuGetFramework> frameworks)
        {
            HashSet<NuGetFramework> result = new HashSet<NuGetFramework>(NuGetFramework.Comparer);

            // add in all framework aliases
            HashSet<NuGetFramework> eqFrameworks = null;
            if (_equivalentFrameworks.TryGetValue(framework, out eqFrameworks))
            {
                foreach (var eqFw in eqFrameworks)
                {
                    result.Add(eqFw);
                }
            }

            var baseFrameworks = new List<NuGetFramework>(result);
            baseFrameworks.Add(framework);

            // add in all profile aliases
            foreach (var fw in baseFrameworks)
            {
                Dictionary<string, HashSet<string>> eqProfiles = null;
                if (_equivalentProfiles.TryGetValue(fw.Framework, out eqProfiles))
                {
                    HashSet<string> matchingProfiles = null;
                    if (eqProfiles.TryGetValue(fw.Profile, out matchingProfiles))
                    {
                        foreach (var eqProfile in matchingProfiles)
                        {
                            result.Add(new NuGetFramework(fw.Framework, fw.Version, eqProfile));
                        }
                    }
                }
            }

            // do not include the original framework
            result.Remove(framework);

            frameworks = result;
            return result.Count > 0;
        }

        public bool TryGetEquivalentFrameworks(FrameworkRange range, out IEnumerable<NuGetFramework> frameworks)
        {
            if (range == null)
            {
                throw new ArgumentNullException("range");
            }

            HashSet<NuGetFramework> relevant = new HashSet<NuGetFramework>(NuGetFramework.Comparer);

            foreach (var framework in _equivalentFrameworks.Keys.Where(f => range.Satisfies(f)))
            {
                relevant.Add(framework);
            }

            HashSet<NuGetFramework> results = new HashSet<NuGetFramework>(NuGetFramework.Comparer);

            foreach (var framework in relevant)
            {
                IEnumerable<NuGetFramework> values = null;
                if (TryGetEquivalentFrameworks(framework, out values))
                {
                    foreach (var val in values)
                    {
                        results.Add(val);
                    }
                }
            }

            frameworks = results;
            return results.Count > 0;
        }

        private void InitMappings(IEnumerable<IFrameworkMappings> mappings)
        {
            if (mappings != null)
            {
                foreach (IFrameworkMappings mapping in mappings)
                {
                    // eq profiles
                    AddEquivalentProfiles(mapping.EquivalentProfiles);

                    // equivalent frameworks
                    AddEquivalentFrameworks(mapping.EquivalentFrameworks);

                    // add synonyms
                    AddFrameworkSynoyms(mapping.IdentifierSynonyms);

                    // populate short <-> long
                    AddIdentifierShortNames(mapping.IdentifierShortNames);

                    // official profile short names
                    AddProfileShortNames(mapping.ProfileShortNames);

                    // add compatiblity mappings
                    AddCompatibilityMappings(mapping.CompatibilityMappings);

                    // add subset frameworks
                    AddSubSetFrameworks(mapping.SubSetFrameworks);
                }
            }
        }

        private void InitPortableMappings(IEnumerable<IPortableFrameworkMappings> portableMappings)
        {
            if (portableMappings != null)
            {
                foreach (var portableMapping in portableMappings)
                {
                    // populate portable framework names
                    AddPortableProfileMappings(portableMapping.ProfileFrameworks);

                    // populate optional frameworks
                    AddPortableOptionalFrameworks(portableMapping.ProfileOptionalFrameworks);
                }
            }
        }

        private void AddCompatibilityMappings(IEnumerable<OneWayCompatibilityMappingEntry> mappings)
        {
            if (mappings != null)
            {
                foreach (var mapping in mappings)
                {
                    _compatibilityMappings.Add(mapping);
                }
            }
        }

        private void AddSubSetFrameworks(IEnumerable<KeyValuePair<string, string>> mappings)
        {
            if (mappings != null)
            {
                foreach (var mapping in mappings)
                {
                    HashSet<string> subSets = null;
                    if (!_subSetFrameworks.TryGetValue(mapping.Value, out subSets))
                    {
                        subSets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        _subSetFrameworks.Add(mapping.Value, subSets);
                    }

                    subSets.Add(mapping.Key);
                }
            }
        }

        /// <summary>
        /// 2 way per framework profile equivalence
        /// </summary>
        /// <param name="mappings"></param>
        private void AddEquivalentProfiles(IEnumerable<FrameworkSpecificMapping> mappings)
        {
            if (mappings != null)
            {
                foreach (FrameworkSpecificMapping profileMapping in mappings)
                {
                    string frameworkIdentifier = profileMapping.FrameworkIdentifier;
                    string profile1 = profileMapping.Mapping.Key;
                    string profile2 = profileMapping.Mapping.Value;

                    Dictionary<string, HashSet<string>> profileMappings = null;

                    if (!_equivalentProfiles.TryGetValue(frameworkIdentifier, out profileMappings))
                    {
                        profileMappings = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                        _equivalentProfiles.Add(frameworkIdentifier, profileMappings);
                    }

                    HashSet<string> innerMappings1 = null;
                    HashSet<string> innerMappings2 = null;

                    if (!profileMappings.TryGetValue(profile1, out innerMappings1))
                    {
                        innerMappings1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        profileMappings.Add(profile1, innerMappings1);
                    }

                    if (!profileMappings.TryGetValue(profile2, out innerMappings2))
                    {
                        innerMappings2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        profileMappings.Add(profile2, innerMappings2);
                    }

                    innerMappings1.Add(profile2);
                    innerMappings2.Add(profile1);
                }
            }
        }

        /// <summary>
        /// 2 way framework equivalence
        /// </summary>
        /// <param name="mappings"></param>
        private void AddEquivalentFrameworks(IEnumerable<KeyValuePair<NuGetFramework, NuGetFramework>> mappings)
        {
            if (mappings != null)
            {
                foreach (var pair in mappings)
                {
                    // first direction
                    HashSet<NuGetFramework> eqFrameworks = null;

                    if (!_equivalentFrameworks.TryGetValue(pair.Key, out eqFrameworks))
                    {
                        eqFrameworks = new HashSet<NuGetFramework>(NuGetFramework.Comparer);
                        _equivalentFrameworks.Add(pair.Key, eqFrameworks);
                    }

                    eqFrameworks.Add(pair.Value);

                    // reverse direction
                    if (!_equivalentFrameworks.TryGetValue(pair.Value, out eqFrameworks))
                    {
                        eqFrameworks = new HashSet<NuGetFramework>(NuGetFramework.Comparer);
                        _equivalentFrameworks.Add(pair.Value, eqFrameworks);
                    }

                    eqFrameworks.Add(pair.Key);
                }
            }
        }

        private void AddFrameworkSynoyms(IEnumerable<KeyValuePair<string, string>> mappings)
        {
            if (mappings != null)
            {
                foreach (var pair in mappings)
                {
                    if (!_identifierSynonyms.ContainsKey(pair.Key))
                    {
                        _identifierSynonyms.Add(pair.Key, pair.Value);
                    }
                }
            }
        }

        private void AddIdentifierShortNames(IEnumerable<KeyValuePair<string, string>> mappings)
        {
            if (mappings != null)
            {
                foreach (var pair in mappings)
                {
                    string shortName = pair.Value;
                    string longName = pair.Key;

                    if (!_identifierSynonyms.ContainsKey(pair.Value))
                    {
                        _identifierSynonyms.Add(pair.Value, pair.Key);
                    }

                    _identifierShortToLong.Add(shortName, longName);

                    _identifierToShortName.Add(longName, shortName);
                }
            }
        }

        private void AddProfileShortNames(IEnumerable<FrameworkSpecificMapping> mappings)
        {
            if (mappings != null)
            {
                foreach (var profileMapping in mappings)
                {
                    _profilesToShortName.Add(profileMapping.Mapping.Value, profileMapping.Mapping.Key);
                    _profileShortToLong.Add(profileMapping.Mapping.Key, profileMapping.Mapping.Value);
                }
            }
        }

        // Add supported frameworks for each portable profile number
        private void AddPortableProfileMappings(IEnumerable<KeyValuePair<int, NuGetFramework[]>> mappings)
        {
            if (mappings != null)
            {
                foreach (var pair in mappings)
                {
                    HashSet<NuGetFramework> frameworks = null;

                    if (!_portableFrameworks.TryGetValue(pair.Key, out frameworks))
                    {
                        frameworks = new HashSet<NuGetFramework>(NuGetFramework.Comparer);
                        _portableFrameworks.Add(pair.Key, frameworks);
                    }

                    foreach (var fw in pair.Value)
                    {
                        frameworks.Add(fw);
                    }
                }
            }
        }

        // Add optional frameworks for each portable profile number
        private void AddPortableOptionalFrameworks(IEnumerable<KeyValuePair<int, NuGetFramework[]>> mappings)
        {
            if (mappings != null)
            {
                foreach (var pair in mappings)
                {
                    HashSet<NuGetFramework> frameworks = null;

                    if (!_portableOptionalFrameworks.TryGetValue(pair.Key, out frameworks))
                    {
                        frameworks = new HashSet<NuGetFramework>(NuGetFramework.Comparer);
                        _portableOptionalFrameworks.Add(pair.Key, frameworks);
                    }

                    foreach (var fw in pair.Value)
                    {
                        frameworks.Add(fw);
                    }
                }
            }
        }


        public bool TryGetCompatibilityMappings(NuGetFramework framework, out IEnumerable<FrameworkRange> supportedFrameworkRanges)
        {
            supportedFrameworkRanges = _compatibilityMappings.Where(m => m.TargetFrameworkRange.Satisfies(framework)).Select(m => m.SupportedFrameworkRange);

            return supportedFrameworkRanges.Any();
        }

        public bool TryGetSubSetFrameworks(string frameworkIdentifier, out IEnumerable<string> subSetFrameworks)
        {
            HashSet<string> values = null;
            if (_subSetFrameworks.TryGetValue(frameworkIdentifier, out values))
            {
                subSetFrameworks = values;
                return true;
            }

            subSetFrameworks = null;
            return false;
        }
    }
}
