using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Helpers;

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

        public static Solution GetSolution(string solutionName, bool shared = false)
        {
            var rootPath = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var originalSolutionPath = Path.Combine(rootPath, "misc", solutionName);
            if (shared)
            {
                return new Solution(originalSolutionPath);
            }

            var tempSolutionPath = GetLocalTempFolder();
            CopyFolder(originalSolutionPath, tempSolutionPath);
            return new Solution(tempSolutionPath);
        }

        public static string GetLocalTempFolder()
        {
            // This env var can be set by VS load profile
            var basePath = Environment.GetEnvironmentVariable("DNX_LOCAL_TEMP_FOLDER_FOR_TESTING");
            if (string.IsNullOrEmpty(basePath))
            {
                var rootPath = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
                basePath = Path.Combine(rootPath, "artifacts");
            }

            var tempFolderPath = Path.Combine(basePath, Path.GetRandomFileName());
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

        public static string CreateLocalFeed(Solution solution)
        {
            var sdk = DnxSdkFunctionalTestBase.DnxSdks.First()[0] as DnxSdk;
            var feed = GetLocalTempFolder();
            var packOutput = GetLocalTempFolder();

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