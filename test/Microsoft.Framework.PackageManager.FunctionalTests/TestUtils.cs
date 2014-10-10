// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager
{
    public static class TestUtils
    {
        public static DirTree CreateDirTree(string json)
        {
            return new DirTree(json);
        }

        public static string CreateTempDir()
        {
            var tempDirPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirPath);
            return tempDirPath;
        }

        public static int ExecKpm(string krePath, string subcommand, string arguments,
            IDictionary<string, string> environment = null, string workingDir = null)
        {
            string program, commandLine;
            if (PlatformHelper.IsMono)
            {
                program = Path.Combine(krePath, "bin", "kpm");
                commandLine = string.Format("{0} {1}", subcommand, arguments);
            }
            else
            {
                program = "cmd";
                var kpmCmdPath = Path.Combine(krePath, "bin", "kpm.cmd");
                commandLine = string.Format("/C {0} {1} {2}", kpmCmdPath, subcommand, arguments);
            }
            return Exec(program, commandLine, environment, workingDir);
        }

        public static int Exec(string program, string commandLine,
            IDictionary<string, string> environment = null, string workingDir = null)
        {
            var processStartInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                WorkingDirectory = workingDir,
                FileName = program,
                Arguments = commandLine,
            };

            if (environment != null)
            {
                foreach (var pair in environment)
                {
                    processStartInfo.EnvironmentVariables[pair.Key] = pair.Value;
                }
            }

            var process = Process.Start(processStartInfo);
            process.WaitForExit();

            return process.ExitCode;
        }

        public static IEnumerable<string> GetUnpackedKrePaths(string buildArtifactDir)
        {
            var kreNupkgs = new List<string>();
            kreNupkgs.Add(Directory.GetFiles(buildArtifactDir, "KRE-CLR-amd64.*.nupkg", SearchOption.TopDirectoryOnly).First());
            kreNupkgs.Add(Directory.GetFiles(buildArtifactDir, "KRE-CLR-x86.*.nupkg", SearchOption.TopDirectoryOnly).First());
            kreNupkgs.Add(Directory.GetFiles(buildArtifactDir, "KRE-CoreCLR-amd64.*.nupkg", SearchOption.TopDirectoryOnly).First());
            kreNupkgs.Add(Directory.GetFiles(buildArtifactDir, "KRE-CoreCLR-x86.*.nupkg", SearchOption.TopDirectoryOnly).First());
            foreach (var nupkg in kreNupkgs)
            {
                var kreName = Path.GetFileNameWithoutExtension(nupkg);
                var krePath = CreateTempDir();
                System.IO.Compression.ZipFile.ExtractToDirectory(nupkg, krePath);
                yield return krePath;
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
    }
}
