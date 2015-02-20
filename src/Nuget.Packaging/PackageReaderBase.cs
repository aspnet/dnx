using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    /// <summary>
    /// Abstract class that both the zip and folder package readers extend
    /// This class contains the path convetions for both zip and folder readers
    /// </summary>
    public abstract class PackageReaderBase : PackageReaderCoreBase
    {
        private NuspecReader _nuspec;

        public PackageReaderBase()
            : base()
        {

        }

        /// <summary>
        /// Frameworks mentioned in the package.
        /// </summary>
        public IEnumerable<NuGetFramework> GetSupportedFrameworks()
        {
            var libFrameworks = GetLibItems().Select(g => g.TargetFramework).Where(tf => !tf.IsUnsupported).Distinct(NuGetFramework.Comparer);

            // TODO: improve this
            if (!libFrameworks.Any() && GetContentItems().Any())
            {
                return new NuGetFramework[] { NuGetFramework.AgnosticFramework };
            }
            else
            {
                return libFrameworks;
            }
        }

        public IEnumerable<FrameworkSpecificGroup> GetFrameworkItems()
        {
            return Nuspec.GetFrameworkReferenceGroups();
        }

        public IEnumerable<FrameworkSpecificGroup> GetBuildItems()
        {
            return GetFileGroups("build");
        }

        public IEnumerable<FrameworkSpecificGroup> GetToolItems()
        {
            return GetFileGroups("tools");
        }

        public IEnumerable<FrameworkSpecificGroup> GetContentItems()
        {
            return GetFileGroups("content");
        }

        public IEnumerable<PackageDependencyGroup> GetPackageDependencies()
        {
            return Nuspec.GetDependencyGroups();
        }

        public IEnumerable<FrameworkSpecificGroup> GetLibItems()
        {
            return GetFileGroups("lib");
        }

        /// <summary>
        /// True only for assemblies that should be added as references to msbuild projects
        /// </summary>
        private static bool IsReferenceAssembly(string path)
        {
            bool result = false;

            string extension = Path.GetExtension(path);

            if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".dll"))
            {
                if (!path.EndsWith(".resource.dll", StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                }
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".winmd"))
            {
                result = true;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".exe"))
            {
                result = true;
            }

            return result;
        }

        public IEnumerable<FrameworkSpecificGroup> GetReferenceItems()
        {
            IEnumerable<FrameworkSpecificGroup> referenceGroups = Nuspec.GetReferenceGroups();
            List<FrameworkSpecificGroup> fileGroups = new List<FrameworkSpecificGroup>();

            // filter out non reference assemblies
            foreach (var group in GetLibItems())
            {
                fileGroups.Add(new FrameworkSpecificGroup(group.TargetFramework, group.Items.Where(e => IsReferenceAssembly(e))));
            }

            // results
            List<FrameworkSpecificGroup> libItems = new List<FrameworkSpecificGroup>();

            if (referenceGroups.Any())
            {
                // the 'any' group from references, for pre2.5 nuspecs this will be the only group
                var fallbackGroup = referenceGroups.Where(g => g.TargetFramework.Equals(NuGetFramework.AnyFramework)).SingleOrDefault();

                foreach (FrameworkSpecificGroup fileGroup in fileGroups)
                {
                    // check for a matching reference group to use for filtering
                    var referenceGroup = referenceGroups.Where(g => g.TargetFramework.Equals(fileGroup.TargetFramework)).SingleOrDefault();

                    if (referenceGroup == null)
                    {
                        referenceGroup = fallbackGroup;
                    }

                    if (referenceGroup == null)
                    {
                        // add the lib items without any filtering
                        libItems.Add(fileGroup);
                    }
                    else
                    {
                        List<string> filteredItems = new List<string>();

                        foreach (string path in fileGroup.Items)
                        {
                            // reference groups only have the file name, not the path
                            string file = Path.GetFileName(path);

                            if (referenceGroup.Items.Any(s => StringComparer.OrdinalIgnoreCase.Equals(s, file)))
                            {
                                filteredItems.Add(path);
                            }
                        }

                        if (filteredItems.Any())
                        {
                            libItems.Add(new FrameworkSpecificGroup(fileGroup.TargetFramework, filteredItems));
                        }
                    }
                }
            }
            else
            {
                libItems.AddRange(fileGroups);
            }

            return libItems;
        }

        protected sealed override NuspecCoreReaderBase NuspecCore
        {
            get
            {
                return Nuspec;
            }
        }

        protected virtual NuspecReader Nuspec
        {
            get
            {
                if (_nuspec == null)
                {
                    _nuspec = new NuspecReader(GetNuspec());
                }

                return _nuspec;
            }
        }

        protected IEnumerable<FrameworkSpecificGroup> GetFileGroups(string folder)
        {
            Dictionary<NuGetFramework, List<string>> groups = new Dictionary<NuGetFramework, List<string>>(new NuGetFrameworkFullComparer());

            bool isContentFolder = StringComparer.OrdinalIgnoreCase.Equals(folder, PackagingConstants.ContentFolder);
            bool allowSubFolders = true;

            foreach (string path in GetFiles(folder))
            {
                NuGetFramework framework = NuGetFramework.Parse(GetFrameworkFromPath(path, allowSubFolders));

                // Content allows both random folder names and framework folder names.
                // It's nearly impossible to tell the difference and stay consistent over
                // time as the frameworks change, but to make the best attempt we can
                // compare the folder name to the known frameworks
                if (isContentFolder)
                {
                    if (!framework.IsSpecificFramework)
                    {
                        framework = NuGetFramework.AnyFramework;
                    }
                }

                List<string> items = null;
                if (!groups.TryGetValue(framework, out items))
                {
                    items = new List<string>();
                    groups.Add(framework, items);
                }

                items.Add(path);
            }

            foreach (NuGetFramework framework in groups.Keys)
            {
                yield return new FrameworkSpecificGroup(framework, groups[framework]);
            }

            yield break;
        }

        /// <summary>
        /// Return property values for the given key. Case-sensitive.
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        private static IEnumerable<string> GetPropertyValues(IEnumerable<KeyValuePair<string, string>> properties, string key)
        {
            if (properties == null)
            {
                return Enumerable.Empty<string>();
            }

            if (!String.IsNullOrEmpty(key))
            {
                return properties.Select(p => p.Value);
            }

            return properties.Where(p => StringComparer.Ordinal.Equals(p.Key, key)).Select(p => p.Value);
        }

        private static string GetFileName(string path)
        {
            return path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        }

        private static string GetFrameworkFromPath(string path, bool allowSubFolders = false)
        {
            string framework = PackagingConstants.AnyFramework;

            string[] parts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // ignore paths that are too short, and ones that have additional sub directories
            if (parts.Length == 3 || (parts.Length > 3 && allowSubFolders))
            {
                framework = parts[1].ToLowerInvariant();

                // TODO: add support for digit only frameworks
                Match match = PackagingConstants.FrameworkRegex.Match(framework);

                if (!match.Success)
                {
                    // this is not a framework and should be ignored
                    framework = PackagingConstants.AnyFramework;
                }
            }

            return framework;
        }

        protected abstract IEnumerable<string> GetFiles(string folder);
    }
}
