// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager.Publish
{
    public class PublishPackage
    {
        private readonly LibraryDescription _libraryDescription;

        public PublishPackage(LibraryDescription libraryDescription)
        {
            _libraryDescription = libraryDescription;
        }

        public Library Library { get { return _libraryDescription.Identity; } }

        public string TargetPath { get; private set; }

        public void Emit(PublishRoot root)
        {
            root.Reports.Quiet.WriteLine("Using {0} dependency {1}", _libraryDescription.Type, Library);

            var srcNupkgPath = Path.Combine(_libraryDescription.Path, Library.Name + "." + Library.Version + ".nupkg");

            var options = new Microsoft.Framework.PackageManager.Packages.AddOptions
            {
                NuGetPackage = srcNupkgPath,
                SourcePackages = root.TargetPackagesPath,
                Reports = root.Reports
            };

            var packagesAddCommand = new Microsoft.Framework.PackageManager.Packages.AddCommand(options);
            packagesAddCommand.Execute().GetAwaiter().GetResult();
        }
    }
}
