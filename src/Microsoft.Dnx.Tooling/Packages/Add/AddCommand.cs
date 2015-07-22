// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Tooling.Packages
{
    /// <summary>
    /// Summary description for AddCommand
    /// </summary>
    public class AddCommand : PackagesCommand<AddOptions>
    {
        public AddCommand(AddOptions options) : base(options)
        {
        }

        public async Task<bool> Execute()
        {
            Reports = Options.Reports;
            LocalPackages = Options.SourcePackages ??
                NuGetDependencyResolver.ResolveRepositoryPath(Directory.GetCurrentDirectory());

            Options.Reports.Quiet.WriteLine(string.Format("Adding NuGet package {0} to {1}",
                Options.NuGetPackage.Bold(), LocalPackages.Bold()));

            var sw = new Stopwatch();
            sw.Start();

            var library = NuGetPackageUtils.CreateLibraryFromNupkg(Options.NuGetPackage);

            using (var stream = File.OpenRead(Options.NuGetPackage))
            {
                await NuGetPackageUtils.InstallFromStream(stream, library, LocalPackages, Reports.Quiet);
            }

            Reports.Quiet.WriteLine(
                "{0}, {1}ms elapsed",
                "Add complete".Green().Bold(),
                sw.ElapsedMilliseconds);

            return true;
        }
    }
}