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
            if (Directory.Exists(breadcrumbsFolder))
            {
                _breadcrumbsFolder = breadcrumbsFolder;
            }
            else
            {
                Logger.TraceInformation("Breadcrumbs for servicing will not be written because the breadcrumbs folder ({0}) does not exist.", breadcrumbsFolder);
            }
        }

        public static bool IsPackageServiceable(PackageInfo package)
        {
            // TODO: Figure out what makes a package serviceable
            return false;
        }

        public void CreateBreadcrumb(string packageId, SemanticVersion packageVersion)
        {
            if (string.IsNullOrWhiteSpace(_breadcrumbsFolder))
            {
                return;
            }

            // Create both files for now until we get clear instructions about the format of the name
            CreateBreadcrumbFile(packageId);
            CreateBreadcrumbFile(packageId + "." + packageVersion);
        }

        public void CreateRuntimeBreadcrumb()
        {
#if DNX451
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

                CreateBreadcrumb(runtimeAssembly.GetName().Name, semanticVersion);
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
                ".NET XRE",
                "BreadcrumbStore");

            return breadcrumbsFolder;
        }

        private void CreateBreadcrumbFile(string fileName)
        {
            string fullFilePath = Path.Combine(_breadcrumbsFolder, fileName);

            try
            {
                if (!File.Exists(fullFilePath))
                {
                    File.Create(fullFilePath).Dispose();
                    Logger.TraceInformation("Wrote servicing breadcrumb for {0}", fileName);
                }
            }
            catch (UnauthorizedAccessException exception)
            {
                LogBreadcrumbsCreationFailure(fileName, exception);
            }
            catch (DirectoryNotFoundException exception)
            {
                LogBreadcrumbsCreationFailure(fileName, exception);
            }
        }

        private static void LogBreadcrumbsCreationFailure(string fileName, Exception exception)
        {
            Logger.TraceInformation("Failed to write servicing breadcrumb for {0} because an exception was thrown: {1}", fileName, exception);
        }
    }
}
