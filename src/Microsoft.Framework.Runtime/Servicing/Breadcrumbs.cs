// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using NuGet;

namespace Microsoft.Framework.Runtime.Servicing
{
    public class Breadcrumbs
    {
        private readonly string _breadcrumbsFolder;

        public Breadcrumbs()
            : this(ResolveBreadcrumbsFolder())
        {
        }

        public Breadcrumbs(string breadcrumbsFolder)
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

        public void CreateRuntimeBreadcrumb()
        {
#if ASPNET50
            var runtimeAssembly = typeof(Breadcrumbs).Assembly;
#else
            var runtimeAssembly = typeof(Breadcrumbs).GetTypeInfo().Assembly;
#endif

            var version = runtimeAssembly
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(version))
            {
                var semanticVersion = new SemanticVersion(version);

                LeaveBreadcrumb(runtimeAssembly.GetName().Name, semanticVersion);
            }
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
                "BreadcrumbStore");

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