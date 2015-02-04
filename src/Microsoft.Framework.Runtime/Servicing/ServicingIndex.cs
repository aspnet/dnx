// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet;
using System.Diagnostics;

namespace Microsoft.Framework.Runtime.Servicing
{
    public class ServicingIndex
    {
        string _folderPath;
        Dictionary<EntryKey, Entry> _entries = new Dictionary<EntryKey, Entry>();

        public void Initialize(string folderPath)
        {
            _folderPath = folderPath;
            var indexFilePath = Path.Combine(folderPath, "index.txt");
            if (!File.Exists(indexFilePath))
            {
                Logger.TraceInformation("[{0}]: Servicing index not found at {1}", GetType().Name, indexFilePath);
                return;
            }
            else
            {
                Logger.TraceInformation("[{0}]: Servicing index loaded from {1}", GetType().Name, indexFilePath);
            }
            using (var stream = new FileStream(indexFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                using (var reader = new StreamReader(stream))
                {
                    var lineNumber = 0;
                    for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    {
                        lineNumber += 1;

                        if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }
                        var parts = line.Split(new[] { '=' }, 2);
                        if (parts.Length != 2)
                        {
                            Logger.TraceInformation("[{0}]: {1}({2}): malformed servicing file", GetType().Name, indexFilePath, lineNumber);
                            continue;
                        }
                        parts[0] = parts[0].Trim();
                        parts[1] = parts[1].Trim();
                        if (parts[0].Length == 0 || parts[1].Length == 0)
                        {
                            Logger.TraceInformation("[{0}]: {1}({2}): malformed servicing file", GetType().Name, indexFilePath, lineNumber);
                            continue;
                        }
                        var fields = parts[0].Split(new[] { '|' });
                        if (String.Equals(fields[0], "nupkg", StringComparison.Ordinal))
                        {
                            if (fields.Length != 4)
                            {
                                Logger.TraceInformation("[{0}]: {1}({2}): malformed servicing key", GetType().Name, indexFilePath, lineNumber);
                                continue;
                            }
                            SemanticVersion version;
                            if (!SemanticVersion.TryParseStrict(fields[2], out version))
                            {
                                Logger.TraceInformation("[{0}]: {1}({2}): malformed servicing version ", GetType().Name, indexFilePath, lineNumber);
                                continue;
                            }
                            var key = new EntryKey(fields[1], version);
                            Entry entry;
                            if (!_entries.TryGetValue(key, out entry))
                            {
                                Logger.TraceInformation("[{0}]: Adding entry for {1} {2}", GetType().Name, key.Id, key.Version);
                                entry = new Entry();
                                _entries.Add(key, entry);
                            }
                            entry.Mappings.Add(new EntryMapping(fields[3], parts[1]));
                        }
                    }
                }
            }
        }

        public bool TryGetReplacement(string packageId, SemanticVersion packageVersion, string assetPath, out string replacementPath)
        {
            Entry entry;
            if (_entries.TryGetValue(new EntryKey(packageId, packageVersion), out entry))
            {
                var normalizedAssetPath = assetPath.Replace('\\', '/');
                foreach (var mapping in entry.Mappings)
                {
                    if (string.Equals(normalizedAssetPath, mapping.AssetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        replacementPath = Path.Combine(_folderPath, mapping.ReplacementPath);
                        Logger.TraceInformation("[{0}]: Using replacement {4} for {1} {2} {3}", GetType().Name, packageId, packageVersion, assetPath, replacementPath);
                        return true;
                    }
                }
            }
            replacementPath = null;
            return false;
        }

        internal struct EntryKey
        {
            public EntryKey(string id, SemanticVersion version)
            {
                Id = id;
                Version = version;
            }
            public string Id { get; }
            public SemanticVersion Version { get; }
        }

        internal class Entry
        {
            public List<EntryMapping> Mappings { get; } = new List<EntryMapping>();
        }

        internal struct EntryMapping
        {
            public EntryMapping(string assetPath, string replacementPath)
            {
                AssetPath = assetPath;
                ReplacementPath = replacementPath;
            }
            public string AssetPath { get; }
            public string ReplacementPath { get; }
        }
    }
}
