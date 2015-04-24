// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Framework.PackageManager.Packages.Workers
{
    /// <summary>
    /// Summary description for RepositoryPublishers
    /// </summary>
    public static class RepositoryPublishers
    {
        public static IRepositoryPublisher Create(
            string path,
            string accessKey,
            Reports reports)
        {
            Uri uri;
            if (Uri.TryCreate(path, UriKind.Absolute, out uri))
            {
                if (uri.Scheme == "https" || uri.Scheme == "http")
                {
                    return new AzureStorageRepositoryPublisher(path, accessKey)
                    {
                        Reports = reports
                    };
                }
            }
            return new FileSystemRepositoryPublisher(path)
            {
                Reports = reports
            };
        }
    }
}