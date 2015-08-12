// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
using NuGet;

namespace Microsoft.Dnx.Tooling.Publish
{
    public class PublishPackage
    {
        private readonly LibraryDescription _libraryDescription;

        public PublishPackage(LibraryDescription libraryDescription)
        {
            _libraryDescription = libraryDescription;
        }

        public LibraryIdentity Library { get { return _libraryDescription.Identity; } }

        public string TargetPath { get; private set; }

        public async Task Emit(PublishRoot root)
        {
            root.Reports.Quiet.WriteLine("Using {0} dependency {1}", _libraryDescription.Type, Library);

            var packagePathResolver = new DefaultPackagePathResolver(root.SourcePackagesPath);
            var srcNupkgPath = packagePathResolver.GetPackageFilePath(_libraryDescription.Identity.Name, _libraryDescription.Identity.Version);

            var options = new Packages.AddOptions
            {
                NuGetPackage = srcNupkgPath,
                SourcePackages = root.TargetPackagesPath,
            };

            // Mute "packages add" subcommand
            options.Reports = Reports.Constants.NullReports;

            var packagesAddCommand = new Packages.AddCommand(options);
            await packagesAddCommand.Execute();
        }
    }
}
