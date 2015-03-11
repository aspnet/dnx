// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Framework.Runtime.DependencyManagement
{
    public class LockFilePackage : LocalPackage
    {
        private string _nuspecPath;
        private LockFileLibrary _lockFileLibrary;
        private IFileSystem _repositoryRoot;

        public LockFilePackage(IFileSystem repositoryRoot, string nuspecPath, LockFileLibrary lockFileLibrary)
        {
            _repositoryRoot = repositoryRoot;
            _nuspecPath = nuspecPath;
            _lockFileLibrary = lockFileLibrary;
            RecreateManifest();
        }

        private void RecreateManifest()
        {
            Id = _lockFileLibrary.Name;
            Version = _lockFileLibrary.Version;
            //Title = metadata.Title;
            //Authors = metadata.Authors;
            //Owners = metadata.Owners;
            //IconUrl = metadata.IconUrl;
            //LicenseUrl = metadata.LicenseUrl;
            //ProjectUrl = metadata.ProjectUrl;
            //RequireLicenseAcceptance = metadata.RequireLicenseAcceptance;
            // DevelopmentDependency = metadata.DevelopmentDependency;
            //Description = metadata.Description;
            //Summary = metadata.Summary;
            //ReleaseNotes = metadata.ReleaseNotes;
            //Language = metadata.Language;
            //Tags = metadata.Tags;
            DependencySets = _lockFileLibrary.DependencySets; //new List<PackageDependencySet>();// metadata.DependencySets;
            FrameworkAssemblies = _lockFileLibrary.FrameworkAssemblies; //new List<FrameworkAssemblyReference>(); //metadata.FrameworkAssemblies;
            //Copyright = metadata.Copyright;
            PackageAssemblyReferences = _lockFileLibrary.PackageAssemblyReferences; //new List<PackageReferenceSet>();// metadata.PackageAssemblyReferences;
            //MinClientVersion = metadata.MinClientVersion;

            // Ensure tags start and end with an empty " " so we can do contains filtering reliably
            if (!String.IsNullOrEmpty(Tags))
            {
                Tags = " " + Tags + " ";
            }
        }

        public override Stream GetStream()
        {
            throw new NotImplementedException();
        }

        protected override IEnumerable<IPackageAssemblyReference> GetAssemblyReferencesCore()
        {
            return from file in GetFilesBase()
                   where IsAssemblyReference(file.Path)
                   select CreatePackageAssemblyReference(file);
        }

        private IPackageAssemblyReference CreatePackageAssemblyReference(IPackageFile file)
        {
            var physicalfile = new PhysicalPackageFile();
            physicalfile.TargetPath = file.Path;
            return new PhysicalPackageAssemblyReference(physicalfile);
        }

        protected override IEnumerable<IPackageFile> GetFilesBase()
        {
            return _lockFileLibrary.Files;
        }
    }
}