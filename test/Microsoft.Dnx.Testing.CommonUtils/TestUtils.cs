// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Helpers;
using System.Runtime.CompilerServices;

namespace Microsoft.Dnx.Testing
{
    public static class TestUtils
    {
        public static FrameworkName GetFrameworkForRuntimeFlavor(string flavor)
        {
            if (string.Equals("clr", flavor, StringComparison.OrdinalIgnoreCase) ||
                string.Equals("mono", flavor, StringComparison.OrdinalIgnoreCase))
            {
                return FrameworkNameHelper.ParseFrameworkName("dnx451");
            }
            else if (string.Equals("coreclr", flavor, StringComparison.OrdinalIgnoreCase))
            {
                return FrameworkNameHelper.ParseFrameworkName("dnxcore50");
            }

            throw new InvalidOperationException($"Unknown runtime flavor '{flavor}'");
        }

        public static Solution GetSolution<TTest>(DnxSdk sdk, string solutionName, [CallerMemberName]string testName = null)
        {
            var rootPath = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var originalSolutionPath = Path.Combine(rootPath, "misc", solutionName);            
            var tempSolutionPath = GetTestFolder<TTest>(sdk, testName);
            CopyFolder(originalSolutionPath, tempSolutionPath);
            return new Solution(tempSolutionPath);
        }
        public static string GetTempTestFolder<T>(DnxSdk sdk, [CallerMemberName]string testName = null)
        {
            
            return GetTestFolder<T>(sdk, $"{testName}.{Path.GetRandomFileName()}");
        }

        public static string GetTestFolder<T>(DnxSdk sdk, [CallerMemberName]string testName = null)
        {
            // This env var can be set by VS load profile
            var basePath = Environment.GetEnvironmentVariable("DNX_LOCAL_TEMP_FOLDER_FOR_TESTING");
            if (string.IsNullOrEmpty(basePath))
            {
                var rootPath = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
                basePath = Path.Combine(rootPath, "TestOutput");
            }

            var tempFolderPath = Path.Combine(basePath, sdk.FullName, $"{typeof(T).Name}.{testName}");
            Directory.CreateDirectory(tempFolderPath);
            return tempFolderPath;
        }

        public static void CopyFolder(string sourceFolder, string targetFolder)
        {
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            foreach (var filePath in Directory.EnumerateFiles(sourceFolder))
            {
                var fileName = Path.GetFileName(filePath);
                File.Copy(filePath, Path.Combine(targetFolder, fileName));
            }

            foreach (var folderPath in Directory.EnumerateDirectories(sourceFolder))
            {
                var folderName = new DirectoryInfo(folderPath).Name;
                CopyFolder(folderPath, Path.Combine(targetFolder, folderName));
            }
        }

        public static string CreateLocalFeed<TTest>(DnxSdk sdk, Solution solution, [CallerMemberName]string testName = null)
        {
            var feed = GetTestFolder<TTest>(sdk, $"{testName}.{Path.GetRandomFileName()}");
            var packOutput = GetTestFolder<TTest>(sdk, $"{testName}.{Path.GetRandomFileName()}");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();
            foreach (var project in solution.Projects)
            {
                var output = sdk.Dnu.Pack(project.ProjectDirectory, packOutput);
                output.EnsureSuccess();
                sdk.Dnu.PackagesAdd(output.PackagePath, feed).EnsureSuccess();
            }

            return feed;
        }
    }
}