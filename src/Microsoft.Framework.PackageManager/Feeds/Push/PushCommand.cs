using Microsoft.Framework.PackageManager.Feeds.Workers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.Framework.PackageManager.Feeds.Push
{
    /// <summary>
    /// Summary description for PushCommand
    /// </summary>
    public class PushCommand
    {
        public PushCommand(PushOptions options)
        {
            Options = options;
        }

        public PushOptions Options { get; private set; }
        public string LocalPackages { get; private set; }
        public string RemotePackages { get; private set; }
        public IReport Report { get; private set; }

        public bool Execute()
        {
            Report = Options.Report;
            LocalPackages = Options.LocalPackages ?? Directory.GetCurrentDirectory();
            RemotePackages = Options.RemotePackages;

            Options.Report.WriteLine(
                "Pushing artifacts from {0} to {1}",
                LocalPackages.Bold(),
                RemotePackages.Bold());

            var sw = new Stopwatch();
            sw.Start();

            IRepositoryPublisher local = new FileSystemRepositoryPublisher(LocalPackages);
            IRepositoryPublisher remote = new FileSystemRepositoryPublisher(RemotePackages);

            // Recall which next to pushed to remote
            var transmitRecord = FillOut(local.GetRepositoryTransmitRecord());

            int nextIndex;
            if (!transmitRecord.Push.TryGetValue(RemotePackages, out nextIndex))
            {
                nextIndex = 1;
            }

            // Read change index from that point forward
            var changeRecord = FillOut(local.MergeRepositoryChangeRecordsStartingWithIndex(nextIndex));

            if (changeRecord.Add.Any() || changeRecord.Remove.Any())
            {
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
            else
            {
                Report.WriteLine("There are no changes to push");
            }

            Report.WriteLine(
                "{0}, {1}ms elapsed",
                "Push complete".Green().Bold(),
                sw.ElapsedMilliseconds);

            return false;
        }

        private RepositoryTransmitRecord FillOut(RepositoryTransmitRecord record)
        {
            if (record == null)
            {
                record = new RepositoryTransmitRecord();
            }
            if (record.Push == null)
            {
                record.Push = new Dictionary<string, int>();
            }
            if (record == null)
            {
                record.Pull = new Dictionary<string, int>();
            }
            return record;
        }
        private RepositoryChangeRecord FillOut(RepositoryChangeRecord record)
        {
            if (record == null)
            {
                record = new RepositoryChangeRecord();
            }
            if (record.Next == 0)
            {
                record.Next = 1;
            }
            if (record.Add == null)
            {
                record.Add = new List<string>();
            }
            if (record.Remove == null)
            {
                record.Remove = new List<string>();
            }
            return record;
        }
    }
}