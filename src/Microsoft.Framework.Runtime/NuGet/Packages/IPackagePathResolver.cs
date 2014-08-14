// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGet
{
    public interface IPackagePathResolver
    {
        /// <summary>
        /// Gets the physical installation path of a package
        /// </summary>
        string GetInstallPath(string packageId, SemanticVersion version, string configuration);

        /// <summary>
        /// Gets the physical path to the nupkg.sha512 file
        /// </summary>
        /// <returns></returns>
        string GetHashPath(string packageId, SemanticVersion version, string configuration);

        /// <summary>
        /// Gets the phsyical path to the nupkg file
        /// </summary>
        string GetPackageFilePath(string packageId, SemanticVersion version, string configuration);

        /// <summary>
        /// Gets the package directory name
        /// </summary>
        string GetPackageDirectory(string packageId, SemanticVersion version, string configuration);

        string GetPackageFileName(string packageId, SemanticVersion version);
    }
}
