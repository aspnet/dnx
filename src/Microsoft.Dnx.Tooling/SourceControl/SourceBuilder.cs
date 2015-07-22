// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Newtonsoft.Json;
using NuGet;

namespace Microsoft.Dnx.Tooling.SourceControl
{
    internal class SourceBuilder
    {
        private readonly Reports _reports;
        private readonly IPackageBuilder _packageBuilder;
        private readonly Runtime.Project _project;

        public SourceBuilder(Runtime.Project project, IPackageBuilder packageBuilder, Reports buildReport)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }
            if (buildReport == null)
            {
                throw new ArgumentNullException(nameof(buildReport));
            }
            if (packageBuilder == null)
            {
                throw new ArgumentNullException(nameof(packageBuilder));
            }

            _project = project;
            _packageBuilder = packageBuilder;
            _reports = buildReport;
        }

        public bool Build(string outputPath)
        {
            if (_project.Repository == null)
            {
                // Nothing to do
                return true;
            }

            _reports.Information.WriteLine("Embedding repository information...");

            string repositoryType;
            if (!_project.Repository.TryGetValue(Constants.RepoTypeKey, out repositoryType) ||
                string.IsNullOrEmpty(repositoryType))
            {
                WriteError("The project file contains a repository property but the repository type is missing.");
                return false;
            }

            var sourceControlProvider = SourceControlProviderFactory.ResolveProvider(repositoryType, _reports);

            if (sourceControlProvider == null)
            {
                WriteError($"'{repositoryType}' repository type is not supported.");
                return false;
            }

            if (!sourceControlProvider.IsInstalled)
            {
                WriteWarning($"Could not find the client for '{repositoryType}' repositories. The repository information will not be included in the package.");
                return true;
            }

            var projectFolder = _project.ProjectDirectory;

            if (!sourceControlProvider.IsRepository(projectFolder))
            {
                WriteWarning("The project is not under a repository. The repository information will not be included in the package.");
                return true;
            }

            sourceControlProvider.AddMissingSnapshotInformation(projectFolder, _project.Repository);

            string snapshotFileContent = JsonConvert.SerializeObject(_project.Repository, Formatting.Indented);
            var snapshotFile = Path.Combine(
                outputPath,
                Constants.SnapshotInfoFileName);
            File.WriteAllText(snapshotFile, snapshotFileContent);

            _reports.Verbose.WriteLine($"Repository information wrote to '{snapshotFile}'.");

            _packageBuilder.Files.Add(new PhysicalPackageFile()
            {
                SourcePath = snapshotFile,
                TargetPath = Constants.SnapshotInfoFileName
            });

            return true;
        }

        private void WriteError(string message)
        {
            _reports.Error.WriteLine(message.Red());
        }

        private void WriteWarning(string message)
        {
            _reports.Information.WriteLine(message.Yellow());
        }
    }
}
