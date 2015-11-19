// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace Microsoft.Dnx.Tooling.Packages.Workers
{
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
}