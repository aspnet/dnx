// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Dnx.Tooling.SourceControl
{
    internal class GitSourceControlProvider
    {
        private const string Git = "git";

        private const string RepositoryUrlKey = "url";
        private const string CommitHashKey = "commit";
        private const string ProjectPathKey = "path";

        private bool? _isInstalled;

        private readonly Reports _buildReports;

        public GitSourceControlProvider(Reports buildReports)
        {
            if (buildReports == null)
            {
                throw new ArgumentNullException(nameof(buildReports));
            }

            _buildReports = buildReports;
        }

        public bool IsInstalled
        {
            get
            {
                if (!_isInstalled.HasValue)
                {
                    _isInstalled = ProcessUtilities.ExecutableExists(Git);
                }

                return _isInstalled.Value;
            }
        }

        public void AddMissingSnapshotInformation(string folderName, IDictionary<string, string> snapshotInformation)
        {
            if (!snapshotInformation.ContainsKey(RepositoryUrlKey) || string.IsNullOrEmpty(snapshotInformation[RepositoryUrlKey]))
            {
                throw new ArgumentNullException("The repository URL must be specified.");
            }

            if (!snapshotInformation.ContainsKey(CommitHashKey))
            {
                snapshotInformation[CommitHashKey] = GetHeadCommitId(folderName);
            }

            if (!snapshotInformation.ContainsKey(ProjectPathKey))
            {
                var repositoryRoot = GetRepositoryRoot(folderName);

                // Get the path relative to the repo root
                var pathRelativeToRepositoryRoot = Path.GetDirectoryName(folderName)
                    .Substring(repositoryRoot.Length)
                    .Replace('\\', '/')
                    .TrimStart('/');
                snapshotInformation[ProjectPathKey] = pathRelativeToRepositoryRoot;
            }
        }

        private string GetHeadCommitId(string folderName)
        {
            StringBuilder commitId = new StringBuilder();
            bool hasErrors = false;

            if (ProcessUtilities.Execute(
                    Git,
                    arguments: "rev-parse HEAD",
                    workingDirectory: folderName,
                    stdOut: (msg) => { commitId.Append(msg); },
                    stdErr: (msg) => {
                        hasErrors = true;
                        _buildReports.WriteError(msg);
                    })
                || hasErrors)
            {
                return commitId.ToString();
            }

            throw new InvalidOperationException("Failed to get the commit hash.");
        }

        private string GetRepositoryRoot(string folderName)
        {
            StringBuilder repositoryRoot = new StringBuilder();
            bool hasErrors = false;

            if (ProcessUtilities.Execute(
                Git,
                arguments: "rev-parse --show-toplevel",
                workingDirectory: folderName, 
                stdOut: (msg) => { repositoryRoot.Append(msg); },
                stdErr: (msg) => {
                    hasErrors = true;
                    _buildReports.WriteError(msg);
                })
                || hasErrors)
            {
                return repositoryRoot.ToString();
            }

            throw new InvalidOperationException("Failed to resolve the repository root.");
        }

        public string CreateShortFolderName(IDictionary<string, string> snapshotInfo)
        {
            if (!ValidateRepositoryType(snapshotInfo) || 
                !ValidateBuildSnapshotInformation(snapshotInfo))
            {
                return null;
            }

            var repoUrl = snapshotInfo[RepositoryUrlKey];
            var commitHash = snapshotInfo[CommitHashKey];

            string repoName = Path.GetFileNameWithoutExtension(repoUrl);
            string shortHash = commitHash.Substring(0, 8);

            return repoName + shortHash;
        }

        public bool GetSources(string destinationFolder, IDictionary<string, string> snapshotInfo)
        {
            if (!ValidateBuildSnapshotInformation(snapshotInfo))
            {
                return false;
            }

            var repoUrl = snapshotInfo[RepositoryUrlKey];
            var commitHash = snapshotInfo[CommitHashKey];

            _buildReports.WriteInformation($"Cloning from: {repoUrl}");

            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            // First clone
            if (!ProcessUtilities.Execute(
                Git,
                $"clone --recursive {repoUrl} {destinationFolder}",
                workingDirectory: null,
                stdOut: _buildReports.WriteInformation,
                stdErr: _buildReports.WriteError))
            {
                return false;
            }

            _buildReports.WriteInformation($"Resetting to commit hash: {repoUrl}");

            // Then sync to that particular commit
            if (!ProcessUtilities.Execute(
                Git,
                $"reset --hard {commitHash}",
                workingDirectory: destinationFolder,
                stdOut: _buildReports.WriteInformation,
                stdErr: _buildReports.WriteError))
            {
                return false;
            }

            return true;
        }

        public string GetSourceFolderPath(IDictionary<string, string> snapshotInfo)
        {
            if (ValidateBuildSnapshotInformation(snapshotInfo))
            {
                return snapshotInfo[ProjectPathKey];
            }

            return null;
        }

        public bool IsRepository(string folder)
        {
            return ProcessUtilities.Execute(Git, "status", workingDirectory: folder);
        }

        private bool ValidateRepositoryType(IDictionary<string, string> snapshotInfo)
        {
            if (!snapshotInfo.ContainsKey(RepositoryUrlKey))
            {
                _buildReports.WriteError("The repository information is missing the repository URL.");
                return false;
            }

            return true;
        }

        private bool ValidateBuildSnapshotInformation(IDictionary<string, string> snapshotInfo)
        {
            bool noErrors = true;

            if (!snapshotInfo.ContainsKey(CommitHashKey))
            {
                _buildReports.WriteError("The repository information is missing the commit hash.");
                noErrors = false;
            }
            if (!snapshotInfo.ContainsKey(ProjectPathKey))
            {
                _buildReports.WriteError("The repository information is missing the project path.");
                noErrors = false;
            }

            return noErrors;
        }
    }
}
