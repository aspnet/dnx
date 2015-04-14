// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.CommonTestUtils
{
    public static class TestUtils
    {
        public static DisposableDir CreateTempDir()
        {
            var tempDirPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirPath);
            return tempDirPath;
        }

        public static int Exec(string program, string commandLine)
        {
            string stdOut;
            string stdErr;
            int code = Exec(program, commandLine, out stdOut, out stdErr);
            if (code != 0)
            {
                throw new InvalidOperationException($"{program} returned exit code {code}.\n{stdErr}");
            }

            return code;
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
#if DNX451
                    processStartInfo.EnvironmentVariables[pair.Key] = pair.Value;
#else
                    processStartInfo.Environment.Add(pair);
#endif
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

        public static string GetTestArtifactsFolder()
        {
            var kRuntimeRoot = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            return Path.Combine(kRuntimeRoot, "artifacts", "test");
        }

        public static DisposableDir GetRuntimeHomeDir(string flavor, string os, string architecture)
        {
            // The build script creates an unzipped image that can be reused
            var testArtifactDir = GetTestArtifactsFolder();
            var runtimeName = GetRuntimeName(flavor, os, architecture);
            var runtimeHomePath = Path.Combine(testArtifactDir, runtimeName);
            var runtimePath = Path.Combine(runtimeHomePath, "runtimes");

            if (Directory.Exists(runtimePath))
            {
                // Don't dispose this because it's shared across all functional tests
                return new DisposableDir(runtimeHomePath, deleteOnDispose: false);
            }

            // We're running an individual tests
            var buildArtifactDir = GetBuildArtifactsFolder();
            var runtimeNupkg = Directory.GetFiles(
                buildArtifactDir,
                GetRuntimeFilePattern(flavor, os, architecture),
                SearchOption.TopDirectoryOnly).First();
            runtimeHomePath = CreateTempDir();
            runtimeName = Path.GetFileNameWithoutExtension(runtimeNupkg);
            var runtimeRoot = Path.Combine(runtimeHomePath, "runtimes", runtimeName);

            if (!PlatformHelper.IsMono)
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(runtimeNupkg, runtimeRoot);
            }
            else
            {
                Directory.CreateDirectory(runtimeRoot);
                Exec("unzip", runtimeNupkg + " -d " + runtimeRoot);
                // We need to mark these files because unzip doesn't preserve the exec bit
                Exec("chmod", "+x " + Path.Combine(runtimeRoot, "bin", "dnx"));
                Exec("chmod", "+x " + Path.Combine(runtimeRoot, "bin", "dnu"));
            }

            return runtimeHomePath;
        }

        public static IEnumerable<object[]> GetRuntimeComponentsCombinations()
        {
            return GetCoreClrRuntimeComponents().Concat(GetClrRuntimeComponents());
        }

        public static IEnumerable<object[]> GetClrRuntimeComponents()
        {
            if (IsWindows())
            {
                yield return new[] { "clr", "win", "x64" };
                yield return new[] { "clr", "win", "x86" };
                yield break;
            }

            yield return new[] { "mono", null, null };
        }

        public static IEnumerable<object[]> GetCoreClrRuntimeComponents()
        {
            if (IsWindows())
            {
                yield return new[] { "coreclr", "win", "x64" };
                yield return new[] { "coreclr", "win", "x86" };
                yield break;
            }

            // Coming soon!
            // yield return new[] { "coreclr", "linix", "x64" };
            // yield return new[] { "coreclr", "linix", "x86" };
            // yield return new[] { "coreclr", "darwin", "x64" };
            // yield return new[] { "coreclr", "darin", "x86" };
        }

        public static DisposableDir GetTempTestSolution(string name)
        {
            var rootDir = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var sourceSolutionPath = Path.Combine(rootDir, "misc", "DnuWrapTestSolutions", name);
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
#if DNX451
            var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
#else
            var programFilesPath = Environment.GetEnvironmentVariable("PROGRAMFILES(X86)");
#endif

            // On 32-bit Windows
            if (string.IsNullOrEmpty(programFilesPath))
            {
#if DNX451
                programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
#else
                programFilesPath = Environment.GetEnvironmentVariable("PROGRAMFILES");
#endif
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

        private static string GetRuntimeName(string flavor, string os, string architecture)
        {
            // Mono ignores os and architecture
            if (string.Equals(flavor, "mono", StringComparison.OrdinalIgnoreCase))
            {
                return string.Format("{0}mono", Constants.RuntimeNamePrefix);
            }

            return string.Format("{0}{1}-{2}-{3}", Constants.RuntimeNamePrefix, flavor, os, architecture);
        }

        private static string GetRuntimeFilePattern(string flavor, string os, string architecture)
        {
            return GetRuntimeName(flavor, os, architecture) + ".*.nupkg";
        }

        private static bool IsWindows()
        {
#if DNX451
            var p = (int)Environment.OSVersion.Platform;
            return (p != 4) && (p != 6) && (p != 128);
#else
            return PlatformHelper.IsWindows;
#endif
        }
    }
}
