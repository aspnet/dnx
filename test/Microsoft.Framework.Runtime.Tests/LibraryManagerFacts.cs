// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Caching;
using Microsoft.Framework.Runtime.Compilation;
using Xunit;

namespace Microsoft.Framework.Runtime.Tests
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

        private static LibraryManager CreateManager(IEnumerable<ILibraryInformation> libraryInfo = null)
        {
            var frameworkName = new FrameworkName("Net45", new Version(4, 5, 1));
            libraryInfo = libraryInfo ?? new[]
            {
                new LibraryInformation("Mvc.RenderingExtensions", new[] { "Mvc.Rendering" }),
                new LibraryInformation("Mvc.Rendering", new[] { "DI", "Mvc.Core", "HttpAbstractions" }),
                new LibraryInformation("DI", new[] { "Config" }),
                new LibraryInformation("Mvc", new[] { "DI", "Mvc.Core", "HttpAbstractions", "Mvc.RenderingExtensions", "Mvc.ModelBinding" }),
                new LibraryInformation("Mvc.Core", new[] { "DI", "HttpAbstractions", "Mvc.ModelBinding" }),
                new LibraryInformation("Mvc.ModelBinding", new[] { "DI", "HttpAbstractions" }),
                new LibraryInformation("HttpAbstractions", Enumerable.Empty<String>()),
                new LibraryInformation("Hosting", new[] { "DI"}),
                new LibraryInformation("Config", Enumerable.Empty<String>()),
                new LibraryInformation("MyApp", new[] { "DI", "Hosting", "Mvc", "HttpAbstractions" })
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