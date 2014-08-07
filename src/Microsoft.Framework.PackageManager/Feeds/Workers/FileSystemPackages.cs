using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Framework.PackageManager.Feeds.Workers
{
    public interface IRepositoryPublisher
    {
        RepositoryChangeRecord GetRepositoryChangeRecord(int index);

        void StoreRepositoryChangeRecord(int index, RepositoryChangeRecord record);

        RepositoryTransmitRecord GetRepositoryTransmitRecord();

        void StoreRepositoryTransmitRecord(RepositoryTransmitRecord record);

        IEnumerable<string> EnumerateArtifacts(
            Func<string, bool> folderPredicate,
            Func<string, bool> artifactPredicate);

        void ApplyFileChanges(
            RepositoryChangeRecord changeRecord,
            IRepositoryPublisher local);

        Stream ReadArtifactStream(string addFile);
    }

    /// <summary>
    /// Summary description for FileSystemPackages
    /// </summary>
    public class FileSystemRepositoryPublisher : IRepositoryPublisher
    {
        private readonly string _path;

        public FileSystemRepositoryPublisher(string path)
        {
            _path = path;
        }

        public IEnumerable<string> EnumerateArtifacts(
            Func<string, bool> folderPredicate,
            Func<string, bool> artifactPredicate)
        {
            List<string> result = new List<string>();
            EnumerateArtifactsRecursive("", folderPredicate, artifactPredicate, result);
            return result;
        }

        void EnumerateArtifactsRecursive(
            string subPath,
            Func<string, bool> folderPredicate,
            Func<string, bool> artifactPredicate,
            List<string> result)
        {
            foreach (var name in Directory.EnumerateDirectories(Path.Combine(_path, subPath)))
            {
                var directoryName = Path.GetFileName(name);
                var directoryPath = Path.Combine(subPath, directoryName);
                if (folderPredicate(directoryPath))
                {
                    EnumerateArtifactsRecursive(directoryPath, folderPredicate, artifactPredicate, result);
                }
            }
            foreach (var name in Directory.EnumerateFiles(Path.Combine(_path, subPath)))
            {
                var fileName = Path.GetFileName(name);
                var filePath = Path.Combine(subPath, fileName);
                if (artifactPredicate(filePath))
                {
                    result.Add(filePath);
                }
            }
        }

        private T GetFile<T>(string filePath)
        {
            var combinedPath = Path.Combine(_path, filePath);
            if (!File.Exists(combinedPath))
            {
                return default(T);
            }
            var text = File.ReadAllText(combinedPath);
            var result = JsonConvert.DeserializeObject<T>(text);
            return result;
        }

        private void StoreFile<T>(string filePath, T content, bool createNew)
        {
            var text = JsonConvert.SerializeObject(content);

            var combinedPath = Path.Combine(_path, filePath);
            var combinedDirectory = Path.GetDirectoryName(combinedPath);

            Directory.CreateDirectory(combinedDirectory);

            using (var stream = new FileStream(
                combinedPath,
                createNew ? FileMode.CreateNew : FileMode.Create,
                FileAccess.Write,
                FileShare.Delete))
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(text);
                }
            }
        }

        public RepositoryChangeRecord GetRepositoryChangeRecord(int index)
        {
            var changeRecordPath = GetChangeRecordPath(index);
            var record = GetFile<RepositoryChangeRecord>(changeRecordPath);
            return record;
        }

        public void StoreRepositoryChangeRecord(int index, RepositoryChangeRecord record)
        {
            var changeRecordPath = GetChangeRecordPath(index);
            StoreFile(
                changeRecordPath, 
                record, 
                createNew: index != 0);
        }

        private string GetChangeRecordPath(int index)
        {
            return Path.Combine(
                "$feed",
                string.Format("{0:D3}", (index / 1000000) % 1000),
                string.Format("{0:D3}", (index / 1000) % 1000),
                string.Format("{0:D9}.json", index)
            );
        }

        public RepositoryTransmitRecord GetRepositoryTransmitRecord()
        {
            return GetFile<RepositoryTransmitRecord>("$feed/transmit.json");
        }

        public void StoreRepositoryTransmitRecord(RepositoryTransmitRecord record)
        {
            StoreFile(
                "$feed/transmit.json", 
                record, 
                createNew: false);
        }

        public void ApplyFileChanges(RepositoryChangeRecord changeRecord, IRepositoryPublisher source)
        {
            foreach (var removeFile in changeRecord.Remove)
            {
                var removePath = Path.Combine(_path, removeFile);
                if (File.Exists(removePath))
                {
                    File.Delete(removePath);
                }
            }
            foreach (var addFile in changeRecord.Add)
            {
                using (var inputStream = source.ReadArtifactStream(addFile))
                {
                    var addPath = Path.Combine(_path, addFile);
                    var addDirectory = Path.GetDirectoryName(addPath);
                    Directory.CreateDirectory(addDirectory);
                    using (var outputStream = new FileStream(
                        addPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.Delete))
                    {
                        inputStream.CopyTo(outputStream);
                    }
                }
            }
        }

        public Stream ReadArtifactStream(string path)
        {
            var filePath = Path.Combine(_path, path);
            return new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete);
        }
    }

    public static class RepositoryPublisherExtensions
    {
        public static RepositoryChangeRecord MergeRepositoryChangeRecordsStartingWithIndex(this IRepositoryPublisher feed, int index)
        {
            RepositoryChangeRecord resultRecord = null;
            var scanIndex = index;
            for (; ;)
            {
                var scanRecord = feed.GetRepositoryChangeRecord(scanIndex);

                if (scanRecord == null)
                {
                    return resultRecord;
                }

                if (resultRecord == null)
                {
                    resultRecord = scanRecord;
                }
                else
                {
                    resultRecord = Merge(resultRecord, scanRecord);
                }
                scanIndex = resultRecord.Next;
            }
        }

        private static RepositoryChangeRecord Merge(
            RepositoryChangeRecord earlierRecord,
            RepositoryChangeRecord laterRecord)
        {
            var mergedRecord = new RepositoryChangeRecord
            {
                Next = laterRecord.Next
            };

            // merged.add is ((earlier.add - later.remove) + later.add)
            mergedRecord.Add = earlierRecord.Add
                .Except(laterRecord.Remove)
                .Union(laterRecord.Add)
                .Distinct()
                .ToArray();

            // merged.remove is ((earlier.remove + later.remove) - later.add)
            mergedRecord.Remove = earlierRecord.Remove
                .Union(laterRecord.Remove)
                .Except(laterRecord.Add)
                .Distinct()
                .ToArray();

            return mergedRecord;
        }
    }


    public class RepositoryChangeRecord
    {
        public int Next { get; set; }

        public IEnumerable<string> Add { get; set; }

        public IEnumerable<string> Remove { get; set; }
    }

    public class RepositoryTransmitRecord
    {
        public IDictionary<string, int> Push { get; set; }

        public IDictionary<string, int> Pull { get; set; }
    }
}
