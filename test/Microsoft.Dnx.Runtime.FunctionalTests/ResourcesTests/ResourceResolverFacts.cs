// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Runtime.Internal;
using Microsoft.Extensions.CompilationAbstractions;
using Xunit;

namespace Microsoft.Dnx.Runtime.FunctionalTests.ResourcesTests
{
    public class ResourceResolverFacts
    {
        [Fact]
        public void ResolveEmbeddedResources()
        {
            var rootDir = ProjectRootResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var testProjectFolder = Path.Combine(rootDir, "misc", "ResourcesTestProjects", "testproject");

            Project project;
            bool projectFound = Project.TryGetProject(testProjectFolder, out project);
            Assert.True(projectFound);

            var resolver = new EmbeddedResourceProvider();
            var embeddedResource = resolver.GetResources(project);

            Assert.Equal("testproject.owntext.txt", embeddedResource[0].Name);
            Assert.Equal("testproject.subfolder.nestedtext.txt", embeddedResource[1].Name);
            Assert.Equal("testproject.OtherText.txt", embeddedResource[2].Name);
        }

        [Fact]
        public void ResolveResxResources()
        {
            var expected = new[]
            {
                "testproject.OwnResources.resources",
                "testproject.subfolder.nestedresource.resources",
                "testproject.OtherResources.resources"
            };
            var rootDir = ProjectRootResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var testProjectFolder = Path.Combine(rootDir, "misc", "ResourcesTestProjects", "testproject");

            Project project;
            bool projectFound = Project.TryGetProject(testProjectFolder, out project);
            Assert.True(projectFound);

            var resolver = new ResxResourceProvider();
            var embeddedResources = resolver.GetResources(project).Select(resource => resource.Name).ToArray();

            Assert.Equal(expected, embeddedResources);
        }

        [Fact]
        public void ResolveRenamedResxResources()
        {
            var rootDir = ProjectRootResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var testProjectFolder = Path.Combine(rootDir, "misc", "ResourcesTestProjects", "testproject");

            Project project = ProjectUtilities.GetProject(@"
{
    ""namedResource"": {
        ""renamedResource"": ""subfolder/nestedresource.resx""
    }
}",
                "testproject",
                Path.Combine(testProjectFolder, "project.json"));

            var resolver = new ResxResourceProvider();
            var embeddedResource = resolver.GetResources(project);

            Assert.Equal("testproject.OwnResources.resources", embeddedResource[0].Name);

            // This resource should get a new name instead of "testproject.subfolder.nestedresource.resources"
            Assert.Equal("renamedResource.resources", embeddedResource[1].Name);
        }

        [Fact]
        public void ResolveNewNamedResxResources()
        {
            var expected = new[]
            {
                "testproject.OwnResources.resources",
                "testproject.subfolder.nestedresource.resources",
                "thisIs.New.Resource.resources"
            };
            var rootDir = ProjectRootResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var testProjectFolder = Path.Combine(rootDir, "misc", "ResourcesTestProjects", "testproject");

            Project project = ProjectUtilities.GetProject(@"
{
    ""namedResource"": {
        ""thisIs.New.Resource"": ""../someresources/OtherResources.resx""
    }
}",
                "testproject",
                Path.Combine(testProjectFolder, "project.json"));

            var resolver = new ResxResourceProvider();
            var embeddedResources = resolver.GetResources(project).Select(resource => resource.Name).ToArray();

            Assert.Equal(expected, embeddedResources);
        }
    }
}
