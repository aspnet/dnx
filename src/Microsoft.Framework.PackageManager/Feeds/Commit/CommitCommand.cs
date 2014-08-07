using Microsoft.Framework.PackageManager.Feeds.Workers;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.Framework.PackageManager.Feeds.Commit
{
    /// <summary>
    /// Summary description for CommitCommand
    /// </summary>
    public class CommitCommand
    {
        public CommitCommand(CommitOptions options)
        {
            Options = options;
        }

        public CommitOptions Options { get; private set; }

        IReport Report { get; set; }
        string LocalPackages { get; set; }

        public bool Execute()
        {
            Report = Options.Report;
            LocalPackages = Options.LocalPackages ?? Directory.GetCurrentDirectory();

            Options.Report.WriteLine(string.Format("Committing artifacts in {0}", LocalPackages.Bold()));

            var sw = new Stopwatch();
            sw.Start();

            IRepositoryPublisher local = new FileSystemRepositoryPublisher(LocalPackages);

            // Read entire index starting with 1
            var index = local.MergeRepositoryChangeRecordsStartingWithIndex(1);
            if (index == null)
            {
                // no index at all - provide a fake "empty before 1" record
                index = new RepositoryChangeRecord
                {
                    Next = 1,
                    Add = new string[0],
                    Remove = new string[0],
                };
            }

            // Read file system
            var artifacts = local.EnumerateArtifacts(FolderPredicate, FilePredicate);

            // Determine difference of index and file system
            var record = new RepositoryChangeRecord
            {
                Next = index.Next + 1,
                Add = artifacts.Except(index.Add).ToArray(),
                Remove = index.Add.Except(artifacts).ToArray()
            };

            // Create new index entry if not empty
            if (record.Add.Any() || record.Remove.Any())
            {
                Report.WriteLine(
                    "Creating record #{0}, {1} artifacts added, {2} artifacts removed",
                    index.Next,
                    record.Add.Count(),
                    record.Remove.Count());

                local.StoreRepositoryChangeRecord(index.Next, record);
            }

            Report.WriteLine(
                "{0}, {1}ms elapsed",
                "Commit complete".Green().Bold(),
                sw.ElapsedMilliseconds);

            return false;
        }

        private bool FolderPredicate(string folderPath)
        {
            if (folderPath.StartsWith("$", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return true;
        }

        private bool FilePredicate(string filePath)
        {
            if (filePath.StartsWith("$", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach(var extension in InnerJoin(
                new[] { ".nupkg", ".nuspec" },
                new[] { "", ".asc" },
                new[] { "", ".sha256", ".sha512" }))
            {
                if (filePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private IEnumerable<string> InnerJoin(params string[][] sets)
        {
            List<string> result = new List<string> { "" };
            foreach(var set in sets)
            {
                result = result.SelectMany(a => set.Select(b => a + b)).ToList();
            }
            return result;
        }
    }
}