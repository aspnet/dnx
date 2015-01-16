// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet;

namespace Microsoft.Framework.Runtime.Servicing
{
    internal class Breadcrumbs
    {
        private readonly string _breadcrumbsFolder;

        public Breadcrumbs()
            : this(ResolveBreadcrumbsFolder())
        {
        }

        internal Breadcrumbs(string breadcrumbsFolder)
        {
            // If the directory doesn't exist, don't create it because it
            // needs special permissions on it
            _breadcrumbsFolder = Directory.Exists(breadcrumbsFolder) ? breadcrumbsFolder : null;
        }

        public static bool IsPackageServiceable(IPackage package)
        {
            // TODO: Figure out what makes a package serviceable
            return false;
        }

        public void LeaveBreadcrumb(string packageId, SemanticVersion packageVersion)
        {
            if (string.IsNullOrWhiteSpace(_breadcrumbsFolder))
            {
                return;
            }

            // Create both files for now until we get clear instructions about the format of the name
            CreateBreadcrumbfile(packageId);
            CreateBreadcrumbfile(packageId + "." + packageVersion);
        }

        private static string ResolveBreadcrumbsFolder()
        {
            var programDataFolder = Environment.GetEnvironmentVariable("ProgramData");
            if (string.IsNullOrWhiteSpace(programDataFolder))
            {
                return null;
            }

            string breadcrumbsFolder = Path.Combine(
                programDataFolder,
                "Microsoft",
                "KRE",
                "breadcrumbs");

            return breadcrumbsFolder;
        }

        private void CreateBreadcrumbfile(string fileName)
        {
            string fullFilePath = Path.Combine(_breadcrumbsFolder, fileName);

            try
            {
                if (!File.Exists(fullFilePath))
                {
                    File.Create(fullFilePath).Dispose();
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
    }
}