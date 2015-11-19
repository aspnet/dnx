// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    public class ProjectContextFacts
    {
        [Fact]
        public void DefaultConstructorSetAllPropertiesNull()
        {
            var target = new ProjectContext();

            // nothing is set
            Assert.Null(target.Configuration);
            Assert.Null(target.Name);
            Assert.Null(target.ProjectDirectory);
            Assert.Null(target.ProjectFilePath);
            Assert.Null(target.TargetFramework);
            Assert.Null(target.Version);
        }
    }
}
