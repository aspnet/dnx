// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGet
{
    public interface IPackagePathResolver
    {
        /// <summary>
        /// Gets the physical installation path of a package
        /// </summary>
        string GetInstallPath(string packageId, SemanticVersion version);

        /// <summary>
        /// Gets the physical path to the nupkg.sha512 file
        /// </summary>
        /// <returns></returns>
        string GetHashPath(string packageId, SemanticVersion version);

        /// <summary>
        /// Gets the phsyical path to the nupkg file
        /// </summary>
        string GetPackageFilePath(string packageId, SemanticVersion version);

        /// <summary>
        /// Gets the phsyical path to the nuspec file
        /// </summary>
        string GetManifestFilePath(string packageId, SemanticVersion version);

        /// <summary>
        /// Gets the package directory name
        /// </summary>
        string GetPackageDirectory(string packageId, SemanticVersion version);

        /// <summary>
        /// Gets file name of the nupkg file
        /// </summary>
        string GetPackageFileName(string packageId, SemanticVersion version);

        /// <summary>
        /// Gets file name of the nuspec file
        /// </summary>
        string GetManifestFileName(string packageId, SemanticVersion version);
    }
}
