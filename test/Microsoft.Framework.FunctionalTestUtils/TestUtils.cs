// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.FunctionalTestUtils
{
    public static class TestUtils
    {
        public static DirTree CreateDirTree(string json)
        {
            return new DirTree(json);
        }

        public static DisposableDirPath CreateTempDir()
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

        public static DisposableDirPath GetUnpackedKrePath(string flavor, string architecture)
        {
            var buildArtifactDir = GetBuildArtifactsFolder();
            var kreNupkg = Directory.GetFiles(
                buildArtifactDir,
                string.Format("KRE-{0}-{1}.*.nupkg", flavor, architecture),
                SearchOption.TopDirectoryOnly) .First();
            var krePath = CreateTempDir();
            System.IO.Compression.ZipFile.ExtractToDirectory(kreNupkg, krePath);
            return krePath;
        }

        public static IEnumerable<DisposableDirPath> GetUnpackedKrePaths()
        {
            yield return GetUnpackedKrePath(flavor: "CLR", architecture: "amd64");
            yield return GetUnpackedKrePath(flavor: "CLR", architecture: "x86");
            yield return GetUnpackedKrePath(flavor: "CoreCLR", architecture: "amd64");
            yield return GetUnpackedKrePath(flavor: "CoreCLR", architecture: "x86");
        }

        public static DisposableDirPath GetTempTestSolution(string name)
        {
            var rootDir = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var sourceSolutionPath = Path.Combine(rootDir, "misc", "KpmWrapTestSolutions", name);
            var targetSolutionPath = CreateTempDir();
            CopyFolder(sourceSolutionPath, targetSolutionPath);
            return targetSolutionPath;
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

        public static string GetKreVersion()
        {
            var kreNupkg = Directory.EnumerateFiles(GetBuildArtifactsFolder(), "KRE-*.nupkg").FirstOrDefault();
            var kreName = Path.GetFileNameWithoutExtension(kreNupkg);
            var segments = kreName.Split(new[] { '.' }, 2);
            return segments[1];
        }
    }
}
