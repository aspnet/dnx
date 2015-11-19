// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Tooling.Packages.Workers
{
    /// <summary>
    /// Summary description for RepositoryPublishers
    /// </summary>
    public static class RepositoryPublishers
    {
        public static IRepositoryPublisher Create(
            string path,
            Reports reports)
        {
            return new FileSystemRepositoryPublisher(path)
            {
                Reports = reports
            };
        }
    }
}