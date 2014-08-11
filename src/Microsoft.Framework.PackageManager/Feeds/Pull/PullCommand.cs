using Microsoft.Framework.PackageManager.Feeds.Workers;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.Framework.PackageManager.Feeds.Pull
{
    /// <summary>
    /// Summary description for PullCommand
    /// </summary>
    public class PullCommand : FeedCommand<PullOptions>
    {
        public PullCommand(PullOptions options) : base(options)
        {
        }

        public string RemotePackages { get; private set; }

        public string RemoteKey { get; private set; }

        public bool Execute()
        {
            Report = Options.Report;
            LocalPackages = Options.LocalPackages ?? Directory.GetCurrentDirectory();
            RemotePackages = Options.RemotePackages;
            RemoteKey = Options.RemoteKey ?? "";

            Options.Report.WriteLine(
                "Pulling artifacts");
            Options.Report.WriteLine(
                "  from {0}",
                RemotePackages.Bold());
            Options.Report.WriteLine(
                "  to {0}",
                LocalPackages.Bold());

            var sw = new Stopwatch();
            sw.Start();

            IRepositoryPublisher local = new FileSystemRepositoryPublisher(
                LocalPackages);

            IRepositoryPublisher remote = RepositoryPublishers.Create(
                RemotePackages,
                RemoteKey,
                Report);

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
                Report.WriteLine("There are no changes to pull");
            }
            else
            {
                Report.WriteLine(
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

            Report.WriteLine(
                "{0}, {1}ms elapsed",
                "Pull complete".Green().Bold(),
                sw.ElapsedMilliseconds);

            return true;
        }
    }
}