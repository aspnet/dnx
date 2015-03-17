// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.FunctionalTestUtils
{
    public static class TestUtils
    {
        public static DisposableDir CreateTempDir()
        {
            var tempDirPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirPath);
            return tempDirPath;
        }

        public static int Exec(
            string program,
            string commandLine,
            out string stdOut,
            out string stdErr,
            IDictionary<string, string> environment = null,
            string workingDir = null)
        {
            var processStartInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                WorkingDirectory = workingDir,
                FileName = program,
                Arguments = commandLine,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (environment != null)
            {
                foreach (var pair in environment)
                {
                    processStartInfo.EnvironmentVariables[pair.Key] = pair.Value;
                }
            }

            var process = Process.Start(processStartInfo);
            stdOut = process.StandardOutput.ReadToEnd();
            stdErr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode;
        }

        public static string GetBuildArtifactsFolder()
        {
            var kRuntimeRoot = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            return Path.Combine(kRuntimeRoot, "artifacts", "build");
        }

        public static DisposableDir GetRuntimeHomeDir(string flavor, string os, string architecture)
        {
            var buildArtifactDir = GetBuildArtifactsFolder();
            var runtimeNupkg = Directory.GetFiles(
                buildArtifactDir,
                string.Format(Constants.RuntimeNamePrefix + "{0}-{1}-{2}.*.nupkg", flavor, os, architecture),
                SearchOption.TopDirectoryOnly) .First();
            var runtimeHomePath = CreateTempDir();
            var runtimeName = Path.GetFileNameWithoutExtension(runtimeNupkg);
            var runtimeRoot = Path.Combine(runtimeHomePath, "runtimes", runtimeName);
            System.IO.Compression.ZipFile.ExtractToDirectory(runtimeNupkg, runtimeRoot);
            return runtimeHomePath;
        }

        public static IEnumerable<object[]> GetRuntimeComponentsCombinations()
        {
            yield return new[] { "clr", "win", "x64" };
            yield return new[] { "clr", "win", "x86" };
            yield return new[] { "coreclr", "win", "x64" };
            yield return new[] { "coreclr", "win", "x86" };
        }

        public static DisposableDir GetTempTestSolution(string name)
        {
            var rootDir = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var sourceSolutionPath = Path.Combine(rootDir, "misc", "KpmWrapTestSolutions", name);
            var targetSolutionPath = CreateTempDir();
            CopyFolder(sourceSolutionPath, targetSolutionPath);
            return targetSolutionPath;
        }

        public static string GetXreTestAppPath(string name)
        {
            var rootDir = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            return Path.Combine(rootDir, "misc", "XreTestApps", name);
        }

        public static string GetSamplesFolder()
        {
            var rootDir = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            return Path.Combine(rootDir, "samples");
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

        public static void DeleteFolder(string path)
        {
            var retryNum = 3;
            for (int i = 0; i < retryNum; i++)
            {
                try
                {
                    Directory.Delete(path, recursive: true);
                    return;
                }
                catch (Exception)
                {
                    if (i == retryNum - 1)
                    {
                        throw;
                    }
                }
            }
        }

        public static string ResolveMSBuildPath()
        {
            var envVal = Environment.GetEnvironmentVariable("KRUNTIME_TESTS_MSBUILD_PATH");
            if (!string.IsNullOrEmpty(envVal))
            {
                return envVal;
            }

            return GetDefaultMSBuildPath();
        }

        private static string GetDefaultMSBuildPath()
        {
            var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            // On 32-bit Windows
            if (string.IsNullOrEmpty(programFilesPath))
            {
                programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            }

            return Path.Combine(programFilesPath, "MSBuild", "14.0", "Bin", "MSBuild.exe");
        }

        public static string GetRuntimeVersion()
        {
            var runtimeNupkg = Directory.EnumerateFiles(GetBuildArtifactsFolder(), Constants.RuntimeNamePrefix + "*.nupkg").FirstOrDefault();
            var runtimeName = Path.GetFileNameWithoutExtension(runtimeNupkg);
            var segments = runtimeName.Split(new[] { '.' }, 2);
            return segments[1];
        }

        public static string ComputeSHA(string path)
        {
            using (var sourceStream = File.OpenRead(path))
            {
                var sha512Bytes = SHA512.Create().ComputeHash(sourceStream);
                return Convert.ToBase64String(sha512Bytes);
            }
        }
    }
}
