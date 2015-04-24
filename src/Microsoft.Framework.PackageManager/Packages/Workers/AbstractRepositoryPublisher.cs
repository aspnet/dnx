// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.PackageManager.Packages.Workers
{
    public abstract class AbstractRepositoryPublisher : IRepositoryPublisher
    {
        public Reports Reports { get; set; }

        public abstract IEnumerable<string> EnumerateArtifacts(
            Func<string, bool> folderPredicate,
            Func<string, bool> artifactPredicate);

        protected virtual T GetFile<T>(string filePath)
        {
            using (var stream = ReadArtifactStream(filePath))
            {
                if (stream == null)
                {
                    return default(T);
                }
                using (var reader = new StreamReader(stream))
                {
                    var text = reader.ReadToEnd();
                    var result = JsonConvert.DeserializeObject<T>(text);
                    return result;
                }
            }
        }

        protected virtual void StoreFile<T>(string filePath, T content, bool createNew)
        {
            var text = JsonConvert.SerializeObject(content);

            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream, Encoding.UTF8, 4096, leaveOpen: true))
                {
                    writer.Write(text);
                }
                stream.Position = 0;
                WriteArtifactStream(filePath, stream, createNew);
            }
        }

        protected virtual void StoreFile(string filePath, JObject content, bool createNew)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream, Encoding.UTF8, 4096, leaveOpen: true))
                {
                    using (var jsonWriter = new JsonTextWriter(writer))
                    {
                        content.WriteTo(new JsonTextWriter(writer));
                    }
                }
                stream.Position = 0;
                WriteArtifactStream(filePath, stream, createNew);
            }
        }

        public abstract Stream ReadArtifactStream(string path);

        public abstract void WriteArtifactStream(string path, Stream content, bool createNew);

        public abstract void RemoveArtifact(string path);

        protected virtual string GetChangeRecordPath(int index)
        {
            return Path.Combine(
                "$feed",
                string.Format("{0:D3}", (index / 1000000) % 1000),
                string.Format("{0:D3}", (index / 1000) % 1000),
                string.Format("{0:D9}.json", index)
            );
        }

        public virtual RepositoryChangeRecord GetRepositoryChangeRecord(int index)
        {
            var changeRecordPath = GetChangeRecordPath(index);
            var record = GetFile<RepositoryChangeRecord>(changeRecordPath);
            return record;
        }

        public virtual void StoreRepositoryChangeRecord(int index, RepositoryChangeRecord record)
        {
            var changeRecordPath = GetChangeRecordPath(index);
            StoreFile(
                changeRecordPath,
                record,
                createNew: index != 0);
        }

        public virtual RepositoryTransmitRecord GetRepositoryTransmitRecord()
        {
            return GetFile<RepositoryTransmitRecord>("$feed/transmit.json");
        }

        public virtual void StoreRepositoryTransmitRecord(RepositoryTransmitRecord record)
        {
            StoreFile(
                "$feed/transmit.json",
                record,
                createNew: false);
        }

        public virtual void ApplyFileChanges(RepositoryChangeRecord changeRecord)
        {
            var alterations = changeRecord.Add
                            .Concat(changeRecord.Remove)
                            .Select(FirstTwoParts)
                            .Distinct()
                            .ToLookup(FirstPart);

            var addIds = new List<string>();
            var removeIds = new List<string>();

            foreach (var firstPart in alterations)
            {
                Reports.Information.WriteLine("Working with {0}", firstPart.Key.Bold());

                var addVersions = new List<string>();
                var removeVersions = new List<string>();

                foreach (var firstTwoParts in firstPart)
                {
                    Reports.Information.WriteLine("Working with {0}", firstTwoParts.Bold());

                    var addAssets = changeRecord.Add.SelectMany(After(firstTwoParts));
                    var removeAssets = changeRecord.Remove.SelectMany(After(firstTwoParts));

                    bool addedAllAssets;
                    bool removedAllAssets;
                    ChangeContents(
                        firstTwoParts + "/$content.json",
                        addAssets,
                        removeAssets,
                        out addedAllAssets,
                        out removedAllAssets);

                    if (addedAllAssets)
                    {
                        addVersions.Add(After(firstPart.Key)(firstTwoParts).Single());
                    }
                    else if (removedAllAssets)
                    {
                        removeVersions.Add(After(firstPart.Key)(firstTwoParts).Single());
                    }
                }

                if (addVersions.Any() || removeVersions.Any())
                {
                    bool addedAllVersions;
                    bool removedAllVersions;
                    var versionsRecord = ChangeContents(
                        firstPart.Key + "/$content.json",
                        addVersions,
                        removeVersions,
                        out addedAllVersions,
                        out removedAllVersions);

                    StoreFile(
                        firstPart.Key + "/index.json",
                        new JObject(
                            new JProperty(
                                "versions",
                                new JArray(versionsRecord.Contents.Select(x => new JValue(x))))
                            ),
                        false);

                    if (addedAllVersions)
                    {
                        addIds.Add(firstPart.Key);
                    }
                    else if (removedAllVersions)
                    {
                        removeIds.Add(firstPart.Key);
                    }
                }
            }

            if (addIds.Any() || removeIds.Any())
            {
                bool addedAllIds;
                bool removedAllIds;
                ChangeContents(
                    "$content.json",
                    addIds,
                    removeIds,
                    out addedAllIds,
                    out removedAllIds);

                if (addedAllIds)
                {
                    StoreFile(
                        "index.json",
                        new JObject(
                            new JProperty("version", "3.0.0-beta.1"),
                            new JProperty(
                                "resources",
                                new JArray(
                                    new JObject(
                                        new JProperty("@id", "."),
                                        new JProperty("@type", "PackageBaseAddress/3.0.0"))))),
                        true);
                }
            }
        }

        private RepositoryContentsRecord ChangeContents(
            string nameVersionIndexPath,
            IEnumerable<string> addItems,
            IEnumerable<string> removeItems,
            out bool addedAll,
            out bool removedAll)
        {
            var record = FillOut(GetFile<RepositoryContentsRecord>(nameVersionIndexPath));
            var originalContents = record.Contents;

            record.Contents = originalContents
                .Except(removeItems)
                .Union(addItems)
                .Distinct()
                .ToList();

            addedAll = record.Contents.Any() && originalContents.Count() == 0;
            removedAll = record.Contents.Count() == 0 && originalContents.Any();

            if (removedAll)
            {
                RemoveArtifact(nameVersionIndexPath);
            }
            else
            {
                StoreFile(nameVersionIndexPath, record, createNew: false);
            }

            return record;
        }

        public virtual void ApplyFileChanges(RepositoryChangeRecord changeRecord, IRepositoryPublisher source)
        {
            foreach (var addFile in changeRecord.Add)
            {
                using (var inputStream = source.ReadArtifactStream(addFile))
                {
                    WriteArtifactStream(addFile, inputStream, createNew: false);
                }
            }

            ApplyFileChanges(changeRecord);

            foreach (var removeFile in changeRecord.Remove)
            {
                RemoveArtifact(removeFile);
            }
        }

        private Func<string, IEnumerable<string>> After(string startsWith)
        {
            return path =>
            {
                if (path.StartsWith(startsWith + "/", StringComparison.Ordinal))
                {
                    return new[] { path.Substring(startsWith.Length + 1) };
                }
                return Enumerable.Empty<string>();
            };
        }

        private RepositoryContentsRecord FillOut(RepositoryContentsRecord record)
        {
            if (record == null)
            {
                record = new RepositoryContentsRecord();
            }
            if (record.Contents == null)
            {
                record.Contents = new List<string>();
            }
            return record;
        }

        private string FirstTwoParts(string path)
        {
            var parts = path.Split(new[] { '/' }, 3);
            return string.Join("/", parts.Take(2));
        }
        private string FirstPart(string path)
        {
            var parts = path.Split(new[] { '/' }, 2);
            return parts.First();
        }
    }
}