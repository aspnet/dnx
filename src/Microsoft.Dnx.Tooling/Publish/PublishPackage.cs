// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
using NuGet;

namespace Microsoft.Dnx.Tooling.Publish
{
    public class PublishPackage
    {
        private readonly PackageDescription _package;

        public PublishPackage(PackageDescription package)
        {
            _package = package;
        }

        public LibraryIdentity Library { get { return _package.Identity; } }

        public string TargetPath { get; private set; }

        public async Task Emit(PublishRoot root)
        {
            root.Reports.Quiet.WriteLine("Using {0} dependency {1}", _package.Type, Library);

            var packagePathResolver = new DefaultPackagePathResolver(root.SourcePackagesPath);
            var srcNupkgPath = packagePathResolver.GetPackageFilePath(_package.Identity.Name, _package.Identity.Version);

            var options = new Packages.AddOptions
            {
                NuGetPackage = srcNupkgPath,
                PackageHash = _package.Library.Sha512,
                SourcePackages = root.TargetPackagesPath,
            };

            // Mute "packages add" subcommand
            options.Reports = Reports.Constants.NullReports;

            var packagesAddCommand = new Packages.AddCommand(options);
            await packagesAddCommand.Execute();
        }
    }
}
