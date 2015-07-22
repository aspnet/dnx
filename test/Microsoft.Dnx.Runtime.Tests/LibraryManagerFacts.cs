// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime.Caching;
using Microsoft.Dnx.Compilation;
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

        private static LibraryManager CreateManager(IEnumerable<Library> libraryInfo = null)
        {
            var frameworkName = new FrameworkName("Net45", new Version(4, 5, 1));
            libraryInfo = libraryInfo ?? new[]
            {
                new Library("Mvc.RenderingExtensions", new[] { "Mvc.Rendering" }),
                new Library("Mvc.Rendering", new[] { "DI", "Mvc.Core", "HttpAbstractions" }),
                new Library("DI", new[] { "Config" }),
                new Library("Mvc", new[] { "DI", "Mvc.Core", "HttpAbstractions", "Mvc.RenderingExtensions", "Mvc.ModelBinding" }),
                new Library("Mvc.Core", new[] { "DI", "HttpAbstractions", "Mvc.ModelBinding" }),
                new Library("Mvc.ModelBinding", new[] { "DI", "HttpAbstractions" }),
                new Library("HttpAbstractions", Enumerable.Empty<String>()),
                new Library("Hosting", new[] { "DI"}),
                new Library("Config", Enumerable.Empty<String>()),
                new Library("MyApp", new[] { "DI", "Hosting", "Mvc", "HttpAbstractions" })
            };
            return new LibraryManager(frameworkName,
                                      "Debug",
                                      () => libraryInfo,
                                      new CompositeLibraryExportProvider(Enumerable.Empty<ILibraryExportProvider>()),
                                      new EmptyCache());
        }

        private class EmptyCache : ICache
        {
            public object Get(object key, Func<CacheContext, object, object> factory)
            {
                throw new NotImplementedException();
            }

            public object Get(object key, Func<CacheContext, object> factory)
            {
                throw new NotImplementedException();
            }
        }
    }
}