// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Dnx.Compilation.DesignTime;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Testing.Framework;
using Microsoft.Dnx.Testing.Framework.DesignTimeHost;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests
{
    public class DesignTimeHostCompilerTests : DnxSdkFunctionalTestBase
    {
        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public async Task TestDiscovery(DnxSdk sdk)
        {
            using (var server = sdk.Dth.CreateServer())
            using (var client = server.CreateClient())
            {
                var solution = TestProjectsRepository.EnsureRestoredSolution("DthTestProjects");
                var project = solution.GetProject("EmptyConsoleApp");
                client.Initialize(project.ProjectDirectory);
                client.DrainMessage(7);

                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(new IPEndPoint(IPAddress.Loopback, server.Port));

                var stream = new NetworkStream(socket);
                var compiler = new DesignTimeHostCompiler(stream);

                client.SendPayLoad(1, DthMessageTypes.EnumerateProjectContexts);

                var target = new CompilationTarget("EmptyConsoleApp",
                                                   VersionUtility.ParseFrameworkName("dnx451"),
                                                   "Debug",
                                                   aspect: null);

                var response = await compiler.Compile(project.ProjectDirectory, target);

                Assert.NotNull(response);
                Assert.Empty(response.Diagnostics);
                Assert.NotEmpty(response.AssemblyBytes);
            }
        }
    }
}
