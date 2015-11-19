// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Dnx.Tooling.Packages.Workers
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
            RepositoryChangeRecord changeRecord);

        void ApplyFileChanges(
            RepositoryChangeRecord changeRecord,
            IRepositoryPublisher local);

        Stream ReadArtifactStream(string addFile);
    }
}