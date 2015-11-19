// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace NuGet
{
    public abstract class LocalPackage : IPackage
    {
        private static readonly string[] ExcludedExtensions =
        {
            ".resources.dll",     // ResourceAssembly
            ".ni.dll"             // NativeImage
        };
        private IList<IPackageAssemblyReference> _assemblyReferences;
        private IList<IPackageAssemblyReference> _resourceReferences;

        protected LocalPackage()
        {
            // local packages are typically listed; exception is with those served by NuGet.Server when delist feature is turned on
            Listed = true;
        }

        public string Id
        {
            get;
            set;
        }

        public SemanticVersion Version
        {
            get;
            set;
        }

        public string Title
        {
            get;
            set;
        }

        public IEnumerable<string> Authors
        {
            get;
            set;
        }

        public IEnumerable<string> Owners
        {
            get;
            set;
        }

        public Uri IconUrl
        {
            get;
            set;
        }

        public Uri LicenseUrl
        {
            get;
            set;
        }

        public Uri ProjectUrl
        {
            get;
            set;
        }

        public Uri ReportAbuseUrl
        {
            get
            {
                return null;
            }
        }

        public int DownloadCount
        {
            get
            {
                return -1;
            }
        }

        public bool RequireLicenseAcceptance
        {
            get;
            set;
        }

        public bool DevelopmentDependency
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }

        public string Summary
        {
            get;
            set;
        }

        public string ReleaseNotes
        {
            get;
            set;
        }

        public string Language
        {
            get;
            set;
        }

        public string Tags
        {
            get;
            set;
        }

        public Version MinClientVersion
        {
            get;
            private set;
        }

        public bool IsAbsoluteLatestVersion
        {
            get
            {
                return true;
            }
        }

        public bool IsLatestVersion
        {
            get
            {
                return this.IsReleaseVersion();
            }
        }

        public bool Listed
        {
            get;
            set;
        }

        public DateTimeOffset? Published
        {
            get;
            set;
        }

        public string Copyright
        {
            get;
            set;
        }

        public IEnumerable<PackageDependencySet> DependencySets
        {
            get;
            set;
        }

        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies
        {
            get;
            set;
        }

        public IEnumerable<IPackageAssemblyReference> AssemblyReferences
        {
            get
            {
                if (_assemblyReferences == null)
                {
                    _assemblyReferences = GetAssemblyReferencesCore().ToList();
                }

                return _assemblyReferences;
            }
        }

        public IEnumerable<IPackageAssemblyReference> ResourceReferences
        {
            get
            {
                if (_resourceReferences == null)
                {
                    _resourceReferences = GetResourceReferencesCore().ToList();
                }

                return _resourceReferences;
            }
        }

        public ICollection<PackageReferenceSet> PackageAssemblyReferences
        {
            get;
            set;
        }

        public virtual IEnumerable<FrameworkName> GetSupportedFrameworks()
        {
            return FrameworkAssemblies.SelectMany(f => f.SupportedFrameworks).Distinct();
        }

        public IEnumerable<IPackageFile> GetFiles()
        {
            return GetFilesBase();
        }

        public abstract Stream GetStream();
        
        protected abstract IEnumerable<IPackageFile> GetFilesBase();

        protected abstract IEnumerable<IPackageAssemblyReference> GetAssemblyReferencesCore();

        protected abstract IEnumerable<IPackageAssemblyReference> GetResourceReferencesCore();

        protected void ReadManifest(Stream manifestStream)
        {
            Manifest manifest = Manifest.ReadFrom(manifestStream, validateSchema: false);

            IPackageMetadata metadata = manifest.Metadata;

            Id = metadata.Id;
            Version = metadata.Version;
            Title = metadata.Title;
            Authors = metadata.Authors;
            Owners = metadata.Owners;
            IconUrl = metadata.IconUrl;
            LicenseUrl = metadata.LicenseUrl;
            ProjectUrl = metadata.ProjectUrl;
            RequireLicenseAcceptance = metadata.RequireLicenseAcceptance;
            // DevelopmentDependency = metadata.DevelopmentDependency;
            Description = metadata.Description;
            Summary = metadata.Summary;
            ReleaseNotes = metadata.ReleaseNotes;
            Language = metadata.Language;
            Tags = metadata.Tags;
            DependencySets = metadata.DependencySets;
            FrameworkAssemblies = metadata.FrameworkAssemblies;
            Copyright = metadata.Copyright;
            PackageAssemblyReferences = metadata.PackageAssemblyReferences;
            MinClientVersion = metadata.MinClientVersion;

            // Ensure tags start and end with an empty " " so we can do contains filtering reliably
            if (!String.IsNullOrEmpty(Tags))
            {
                Tags = " " + Tags + " ";
            }
        }

        internal protected static bool IsAssemblyReference(string filePath)
        {           
            // assembly reference must be under lib/
            if (!filePath.StartsWith(Constants.LibDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fileName = Path.GetFileName(filePath);

            // if it's an empty folder, yes
            if (fileName == Constants.PackageEmptyFileName)
            {
                return true;
            }

            // Assembly reference must have a .dll|.exe|.winmd extension and is not a resource or native assembly;
            return !ExcludedExtensions.Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) &&
                Constants.AssemblyReferencesExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);
        }

        internal protected static bool IsResourcesReference(string filePath)
        {
            // assembly reference must be under lib/
            if (!filePath.StartsWith(Constants.LibDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fileName = Path.GetFileName(filePath);

            // if it's an empty folder, yes
            if (fileName == Constants.PackageEmptyFileName)
            {
                return true;
            }

            // Assembly reference must have a .dll|.exe|.winmd extension and is not a resource or native assembly;
            return filePath.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase) &&
                Constants.AssemblyReferencesExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            // extension method, must have 'this'.
            return this.GetFullName();
        }
    }
}