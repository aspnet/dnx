// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests.Infrastructure
{
    public class DthTestServer : IDisposable
    {
        private const string DthName = "Microsoft.Dnx.DesignTimeHost";
        private Process _process;

        public static DthTestServer Create(string runtimeHomePath, string projectPath, TimeSpan timeout)
        {
            var runtimeRoot = GetRuntimeRoot(runtimeHomePath);
            var bootstraperExe = Path.Combine(runtimeRoot, "bin", Constants.BootstrapperExeName);

            var dthPath = Path.Combine(runtimeRoot, "bin", "lib", DthName, $"{DthName}.dll");
            if (!File.Exists(dthPath))
            {
                throw new InvalidOperationException($"Can't find {DthName} at {dthPath}.");
            }

            var port = FindFreePort();
            var hostId = Guid.NewGuid().ToString();
            var processStartInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                WorkingDirectory = projectPath,
                FileName = bootstraperExe,
                Arguments = $"--appbase \"{projectPath}\" \"{dthPath}\" {port} {Process.GetCurrentProcess().Id} {hostId}",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var wait = new ManualResetEvent(false);
            var success = false;
            var successOuput = $"Listening on port {port}";

            var process = Process.Start(processStartInfo);
            process.EnableRaisingEvents = true;

            var stdout = new StringBuilder();
            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    stdout.AppendLine(TestUtils.RemoveAnsiColorCodes(args.Data));

                    if (string.Equals(args.Data, successOuput, StringComparison.Ordinal))
                    {
                        success = true;
                        wait.Set();
                    }
                }
            };

            var stderr = new StringBuilder();
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    stderr.AppendLine(TestUtils.RemoveAnsiColorCodes(args.Data));
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (wait.WaitOne(timeout))
            {
                if (!success)
                {
                    Console.WriteLine(stdout);
                    Console.WriteLine(stderr);

                    throw new InvalidOperationException($"Failed to start DesignTime host.");
                }
                else
                {
                    return new DthTestServer(process, port, hostId);
                }
            }
            else
            {
                throw new InvalidOperationException($"Timeout after {timeout.TotalSeconds}. Expected message {successOuput} from STDOUT to indicate that DTH is started successfully.");
            }
        }

        public static DthTestServer Create(string runtimeHomePath, string projectPath)
        {
            return Create(runtimeHomePath, projectPath, TimeSpan.FromSeconds(10));
        }

        public DthTestServer(Process process, int port, string hostId)
        {
            _process = process;
            Port = port;
            HostId = hostId;
        }

        public string HostId { get; }

        public int Port { get; }

        public void Dispose()
        {
            try
            {

                _process?.Kill();
            }
            catch (InvalidOperationException)
            {
                // swallow the exception if the process had been terminated.
            }
        }

        private static string GetRuntimeRoot(string runtimeHomePath)
        {
            var runtimeRoot = string.Empty;
            var dnxDev = Environment.GetEnvironmentVariable("DNX_DEV");
            if (string.Equals(dnxDev, "1"))
            {
                return runtimeHomePath;
            }
            else
            {
                return Directory.EnumerateDirectories(Path.Combine(runtimeHomePath, "runtimes"), Constants.RuntimeNamePrefix + "*").First();
            }
        }

        private static int FindFreePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }
    }
}
