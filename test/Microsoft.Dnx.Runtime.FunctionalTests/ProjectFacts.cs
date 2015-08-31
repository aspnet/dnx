// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.Dnx.Runtime.FunctionalTests
{
    public class ProjectFacts
    {
        [Fact]
        public void TryGetProject_FileDoesntExist()
        {
            Project project;
            bool gotProject = Project.TryGetProject(@"c:\thispathshouldnotexist\project.json", out project);
            Assert.False(gotProject);
        }
    }
}
