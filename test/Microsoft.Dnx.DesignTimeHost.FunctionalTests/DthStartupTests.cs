// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.DesignTimeHost.FunctionalTests.Infrastructure;
using Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages;
using Xunit;

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests
{
    [Collection(nameof(DthFunctionalTestCollection))]
    public class DthStartupTests
    {
        private readonly DthFunctionalTestFixture _fixture;

        public DthStartupTests(DthFunctionalTestFixture fixture)
        {
            _fixture = fixture;
        }

        public static IEnumerable<object[]> ClrRuntimeComponents
        {
            get { return TestUtils.GetClrRuntimeComponents(); }
        }

        [Theory]
        [MemberData(nameof(ClrRuntimeComponents))]
        public void DthStartup_GetProjectInformation(string flavor, string os, string architecture)
        {
            var projectName = "EmptyConsoleApp";
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var testProject = _fixture.GetTestProjectPath(projectName);

            using (var server = DthTestServer.Create(runtimeHomePath, testProject))
            using (var client = new DthTestClient(server, 0))
            {
                client.Initialize(testProject);

                var response = client.GetResponse<ProjectMessage>();
                Assert.NotNull(response);
                Assert.Equal(server.HostId, response.HostId);
                Assert.Equal(0, response.ContextId);
                Assert.Equal("ProjectInformation", response.MessageType);

                var projectInfo = response.Payload;
                Assert.Equal(projectName, projectInfo.Name);
                Assert.Equal(2, projectInfo.Configurations.Count);
                Assert.Contains("Debug", projectInfo.Configurations);
                Assert.Contains("Release", projectInfo.Configurations);

                var frameworkShorNames = projectInfo.Frameworks.Select(f => f.ShortName);
                Assert.Equal(3, frameworkShorNames.Count());
                Assert.Contains("dnxcore50", frameworkShorNames);
                Assert.Contains("dnx451", frameworkShorNames);
                Assert.Contains("net46", frameworkShorNames);
            }
        }
    }
}
