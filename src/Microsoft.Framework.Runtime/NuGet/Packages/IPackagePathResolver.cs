// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGet
{
    public interface IPackagePathResolver
    {
        /// <summary>
        /// Gets the physical installation path of a package
        /// </summary>
        string GetInstallPath(IPackage package);

        /// <summary>
        /// Gets the package directory name
        /// </summary>
        string GetPackageDirectory(IPackage package);

        string GetPackageDirectory(string packageId, SemanticVersion version);

        /// <summary>
        /// Gets the package file name
        /// </summary>
        string GetPackageFileName(IPackage package);

        string GetPackageFileName(string packageId, SemanticVersion version);
    }
}
