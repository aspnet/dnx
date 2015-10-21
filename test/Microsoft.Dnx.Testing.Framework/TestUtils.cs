// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Runtime.Helpers;

namespace Microsoft.Dnx.Testing.Framework
{
    public static class TestUtils
    {
        private static string _rootTestFolder;

        public static string RootTestFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_rootTestFolder))
                {
                    // This env var can be set by VS load profile
                    _rootTestFolder = Environment.GetEnvironmentVariable(TestEnvironmentNames.LocalTestFolder);
                    if (string.IsNullOrEmpty(_rootTestFolder))
                    {
                        var rootPath = ProjectRootResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
                        _rootTestFolder = Path.Combine(rootPath, TestConstants.TestOutputDirectory);
                    }
                }
                return _rootTestFolder;
            }
        }

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

        public static string GetTestSolutionsDirectory()
        {
            var rootPath = ProjectRootResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            return Path.Combine(rootPath, TestConstants.TestSolutionsDirectory);
        }

        public static Solution GetSolution<TTest>(
            DnxSdk sdk, 
            string solutionName, 
            [CallerMemberName]string testName = null, 
            bool appendSolutionNameToTestFolder = false)
        {
            var originalSolutionPath = Path.Combine(GetTestSolutionsDirectory(), solutionName);
            var tempSolutionPath = GetTestFolder<TTest>(sdk, Path.Combine(testName, appendSolutionNameToTestFolder ? solutionName : string.Empty));
            CopyFolder(originalSolutionPath, tempSolutionPath);
            return new Solution(tempSolutionPath);
        }

        public static string GetTempTestFolder<T>(DnxSdk sdk, [CallerMemberName]string testName = null)
        {
            return GetTestFolder<T>(sdk, Path.Combine(testName, Path.GetRandomFileName()));
        }

        public static string GetTestFolder<T>(DnxSdk sdk, [CallerMemberName]string testName = null)
        {
            var tempFolderPath = Path.Combine(RootTestFolder, $"{typeof(T).Name}.{testName}", sdk.ShortName);
            Directory.CreateDirectory(tempFolderPath);
            return tempFolderPath;
        }

        public static void CopyFolder(string sourceFolder, string targetFolder)
        {
            if (Directory.Exists(targetFolder))
            {
                Directory.Delete(targetFolder, recursive: true);
            }

            Directory.CreateDirectory(targetFolder);

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
            var feed = GetTestFolder<TTest>(sdk, Path.Combine(testName, Path.GetRandomFileName()));
            var packOutput = GetTestFolder<TTest>(sdk, Path.Combine(testName, Path.GetRandomFileName()));

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();
            foreach (var project in solution.Projects)
            {
                var output = sdk.Dnu.Pack(project.ProjectDirectory, packOutput);
                output.EnsureSuccess();
                sdk.Dnu.PackagesAdd(output.PackagePath, feed).EnsureSuccess();
            }

            return feed;
        }

        public static void CleanUpTestDir<T>(DnxSdk sdk, [CallerMemberName]string testName = null)
        {
            var saveFilesBehaviour = Environment.GetEnvironmentVariable(TestEnvironmentNames.SaveFiles);
            if (string.IsNullOrEmpty(saveFilesBehaviour) || !saveFilesBehaviour.Equals(TestConstants.SaveFilesAll, StringComparison.OrdinalIgnoreCase))
            {
                var testFolder = GetTestFolder<T>(sdk, testName);
                if (Directory.Exists(testFolder))
                {
                    Directory.Delete(testFolder, recursive: true);
                }
            }
        }
    }
}