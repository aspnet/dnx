// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class LibraryManagerFacts
    {
        [Fact]
        public void GetReferencingLibraries_ReturnsFirstLevelReferences()
        {
            // Arrange
            var manager = CreateManager();

            // Act
            var referencingLibraries = manager.GetReferencingLibraries("Hosting");

            // Assert
            Assert.Equal(new[] { "MyApp" },
                         referencingLibraries.Select(y => y.Name));
        }

        [Theory]
        [InlineData("Mvc.Core", new[] { "Mvc", "Mvc.Rendering", "Mvc.RenderingExtensions", "MyApp" })]
        [InlineData("Config", new[] { "DI", "Hosting", "Mvc", "Mvc.Core", "Mvc.ModelBinding", "Mvc.Rendering", "Mvc.RenderingExtensions", "MyApp" })]
        public void GetReferencingLibraries_ReturnsFullListOfReferences(string library, string[] expectedReferences)
        {
            // Arrange
            var manager = CreateManager();

            // Act
            var referencingLibraries = manager.GetReferencingLibraries(library);

            // Assert
            Assert.Equal(expectedReferences, referencingLibraries.Select(y => y.Name).OrderBy(y => y));
        }

        private static LibraryManager CreateManager()
        {
            var frameworkName = new FrameworkName("Net45", new Version(4, 5, 1));
            var libraryInfo = new[]
            {
                CreateRuntimeLibrary("Mvc.RenderingExtensions", new[] { "Mvc.Rendering" }),
                CreateRuntimeLibrary("Mvc.Rendering", new[] { "DI", "Mvc.Core", "HttpAbstractions" }),
                CreateRuntimeLibrary("DI", new[] { "Config" }),
                CreateRuntimeLibrary("Mvc", new[] { "DI", "Mvc.Core", "HttpAbstractions", "Mvc.RenderingExtensions", "Mvc.ModelBinding" }),
                CreateRuntimeLibrary("Mvc.Core", new[] { "DI", "HttpAbstractions", "Mvc.ModelBinding" }),
                CreateRuntimeLibrary("Mvc.ModelBinding", new[] { "DI", "HttpAbstractions" }),
                CreateRuntimeLibrary("HttpAbstractions", Enumerable.Empty<String>()),
                CreateRuntimeLibrary("Hosting", new[] { "DI"}),
                CreateRuntimeLibrary("Config", Enumerable.Empty<String>()),
                CreateRuntimeLibrary("MyApp", new[] { "DI", "Hosting", "Mvc", "HttpAbstractions" })
            };
            return new LibraryManager("/foo/project.json", frameworkName, libraryInfo);
        }

        private static LibraryDescription CreateRuntimeLibrary(string name, IEnumerable<string> dependencies)
        {
            var version = new SemanticVersion("1.0.0");
            return new LibraryDescription(
                new LibraryRange(name, frameworkReference: false),
                new LibraryIdentity(name, version, isGacOrFrameworkReference: false),
                "Test",
                LibraryTypes.Package,
                dependencies.Select(d => new LibraryDependency()
                {
                    Library = new LibraryDescription(
                        new LibraryRange(d, frameworkReference: false),
                        new LibraryIdentity(d, version, isGacOrFrameworkReference: false),
                        path: $"/{d}",
                        type: LibraryTypes.Package,
                        dependencies: Enumerable.Empty<LibraryDependency>(),
                        assemblies: Enumerable.Empty<string>(),
                        framework: null),
                    LibraryRange = new LibraryRange(d, frameworkReference: false)
                }),
                assemblies: Enumerable.Empty<string>(),
                framework: null);
        }
    }
}