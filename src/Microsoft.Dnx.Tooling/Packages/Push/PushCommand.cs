// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Tooling.Packages.Workers;

namespace Microsoft.Dnx.Tooling.Packages
{
    /// <summary>
    /// Summary description for PushCommand
    /// </summary>
    public class PushCommand : PackagesCommand<PushOptions>
    {
        public PushCommand(PushOptions options) : base(options)
        {
        }

        public string RemotePackages { get; private set; }

        public bool Execute()
        {
            Reports = Options.Reports;
            LocalPackages = Options.SourcePackages ?? Directory.GetCurrentDirectory();
            RemotePackages = Options.RemotePackages;

            Options.Reports.Information.WriteLine(
                "Pushing artifacts");
            Options.Reports.Information.WriteLine(
                "  from {0}",
                LocalPackages.Bold());
            Options.Reports.Information.WriteLine(
                "  to {0}",
                RemotePackages.Bold());

            var sw = new Stopwatch();
            sw.Start();

            IRepositoryPublisher local = new FileSystemRepositoryPublisher(
                LocalPackages);

            IRepositoryPublisher remote = RepositoryPublishers.Create(
                RemotePackages,
                Reports);

            // Recall what index to start pushing to remote
            var transmitRecord = FillOut(local.GetRepositoryTransmitRecord());

            int nextIndex;
            if (!transmitRecord.Push.TryGetValue(RemotePackages, out nextIndex))
            {
                nextIndex = 1;
            }

            // Read change index from that point forward
            var changeRecord = FillOut(local.MergeRepositoryChangeRecordsStartingWithIndex(nextIndex));

            if (!changeRecord.Add.Any() &&
                !changeRecord.Remove.Any())
            {
                Reports.Information.WriteLine("There are no changes to push");
            }
            else
            {
                Reports.Information.WriteLine(
                    "Pushing {0} added and {1} removed artifacts",
                    changeRecord.Add.Count().ToString().Bold(),
                    changeRecord.Remove.Count().ToString().Bold());

                // We now know where to start from next time
                transmitRecord.Push[RemotePackages] = changeRecord.Next;

                // Determine the latest remote change number
                var remoteZeroRecord = FillOut(remote.GetRepositoryChangeRecord(0));
                var remoteIndexNext = remoteZeroRecord.Next;

                // Point remote records to point to the following remote change number
                changeRecord.Next = remoteIndexNext + 1;
                remoteZeroRecord.Next = remoteIndexNext + 1;

                // Apply the file changes to remote
                remote.ApplyFileChanges(changeRecord, local);

                // Correct /{id}/{version}/$index.json files based on file changes

                // Commit new change record to remote
                remote.StoreRepositoryChangeRecord(0, remoteZeroRecord);
                remote.StoreRepositoryChangeRecord(remoteIndexNext, changeRecord);

                // Locally commit where to push remotely next
                local.StoreRepositoryTransmitRecord(transmitRecord);
            }

            Reports.Information.WriteLine(
                "{0}, {1}ms elapsed",
                "Push complete".Green().Bold(),
                sw.ElapsedMilliseconds);

            return true;
        }
    }
}