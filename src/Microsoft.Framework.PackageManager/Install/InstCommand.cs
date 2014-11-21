// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using System.IO;

namespace Microsoft.Framework.PackageManager.Install
{
    public class InstCommand
    {
        public InstCommand(IApplicationEnvironment env)
        {
            RestoreCommand = new RestoreCommand(env);
        }

        public RestoreCommand RestoreCommand { get; private set; }

        public Reports Reports
        {
            get { return RestoreCommand.Reports; }
            set { RestoreCommand.Reports = value; }
        }

        public FeedOptions FeedOptions
        {
            get { return RestoreCommand.FeedOptions; }
            set { RestoreCommand.FeedOptions = value; }
        }

        public string PackageId
        {
            get { return RestoreCommand.RestorePackageId; }
            set { RestoreCommand.RestorePackageId = value; }
        }

        public string PackageVersion
        {
            get { return RestoreCommand.RestorePackageVersion; }
            set { RestoreCommand.RestorePackageVersion = value; }
        }

        public async Task<bool> ExecuteCommand()
        {
            if (string.IsNullOrEmpty(FeedOptions.PackageFolder))
            {
                var kpmFolder = Path.Combine(
                        Environment.GetEnvironmentVariable("USERPROFILE"),
                        ".kpm");

                var installPackageFolder = Path.Combine(
                    kpmFolder,
                    "bin");

                var installGlobalJson = Path.Combine(
                    installPackageFolder,
                    "global.json");

                if (!File.Exists(installGlobalJson))
                {
                    Directory.CreateDirectory(installPackageFolder);
                    File.WriteAllText(installGlobalJson, @"{""packages"":"".""}");
                }

                FeedOptions.PackageFolderOptions.Values.Add(installPackageFolder);
            }


            if (PackageId != null &&
                PackageId.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(PackageId))
            {
                var packagePath = Path.GetFullPath(PackageId);
                var packageDirectory = Path.GetDirectoryName(packagePath);
                var zipPackage = new NuGet.ZipPackage(packagePath);
                FeedOptions.FallbackSourceOptions.Values.Add(packageDirectory);
                PackageId = zipPackage.Id;
                PackageVersion = zipPackage.Version.ToString();
            }

            RestoreCommand.ReadSettings(FeedOptions.PackageFolder);
            var success = await RestoreCommand.RestoreForInstall(FeedOptions.PackageFolder);

            var toolsFolder = Path.Combine(FeedOptions.PackageFolder, PackageId, PackageVersion, "app");
            if (Directory.Exists(toolsFolder))
            {
                foreach (var commandFile in Directory.EnumerateFiles(toolsFolder, "*.cmd"))
                {
                    var linkFile = Path.Combine(FeedOptions.PackageFolder, Path.GetFileName(commandFile));
                    var targetFile = Path.Combine(PackageId, PackageVersion, "app", Path.GetFileName(commandFile));
                    File.WriteAllText(linkFile, string.Format(@"@""%~dp0{0}"" %*
", targetFile));
                }
            }

            return success;
        }
    }
}
