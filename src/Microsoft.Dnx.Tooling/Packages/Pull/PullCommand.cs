// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Tooling.Packages.Workers;

namespace Microsoft.Dnx.Tooling.Packages
{
    /// <summary>
    /// Summary description for PullCommand
    /// </summary>
    public class PullCommand : PackagesCommand<PullOptions>
    {
        public PullCommand(PullOptions options) : base(options)
        {
        }

        public string RemotePackages { get; private set; }

        public bool Execute()
        {
            Reports = Options.Reports;
            LocalPackages = Options.SourcePackages ?? Directory.GetCurrentDirectory();
            RemotePackages = Options.RemotePackages;

            Options.Reports.Information.WriteLine(
                "Pulling artifacts");
            Options.Reports.Information.WriteLine(
                "  from {0}",
                RemotePackages.Bold());
            Options.Reports.Information.WriteLine(
                "  to {0}",
                LocalPackages.Bold());

            var sw = new Stopwatch();
            sw.Start();

            IRepositoryPublisher local = RepositoryPublishers.Create(
                LocalPackages,
                reports: Reports);

            IRepositoryPublisher remote = RepositoryPublishers.Create(
                RemotePackages,
                Reports);

            // Recall what index to start pulling from remote
            var transmitRecord = FillOut(local.GetRepositoryTransmitRecord());

            int nextIndex;
            if (!transmitRecord.Pull.TryGetValue(RemotePackages, out nextIndex))
            {
                nextIndex = 1;
            }

            // Read change index from that point forward
            var changeRecord = FillOut(remote.MergeRepositoryChangeRecordsStartingWithIndex(nextIndex));

            if (!changeRecord.Add.Any() &&
                !changeRecord.Remove.Any())
            {
                Reports.Information.WriteLine("There are no changes to pull");
            }
            else
            {
                Reports.Information.WriteLine(
                    "Pulling {0} added and {1} removed artifacts",
                    changeRecord.Add.Count().ToString().Bold(),
                    changeRecord.Remove.Count().ToString().Bold());

                // We now know where to start from next time
                transmitRecord.Pull[RemotePackages] = changeRecord.Next;

                // Determine the next local change number
                var localZeroRecord = FillOut(local.GetRepositoryChangeRecord(0));
                var localIndexNext = localZeroRecord.Next;

                // Point local records at the new next local change number
                changeRecord.Next = localIndexNext + 1;
                localZeroRecord.Next = localIndexNext + 1;

                // Apply the file changes to local
                local.ApplyFileChanges(changeRecord, remote);

                // Correct /{id}/{version}/$index.json files based on file changes

                // Commit new change record to remote
                local.StoreRepositoryChangeRecord(0, localZeroRecord);
                local.StoreRepositoryChangeRecord(localIndexNext, changeRecord);

                // Locally commit where to push remotely next
                local.StoreRepositoryTransmitRecord(transmitRecord);
            }

            Reports.Information.WriteLine(
                "{0}, {1}ms elapsed",
                "Pull complete".Green().Bold(),
                sw.ElapsedMilliseconds);

            return true;
        }
    }
}