// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.CompilationAbstractions;
using Xunit;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    public class BeforeCompileContextFacts
    {
        [Fact]
        public void DefaultConstructorSetAllPropertiesNull()
        {
            var target = new BeforeCompileContext();

            // nothing is set
            Assert.Null(target.Compilation);
            Assert.Null(target.Diagnostics);
            Assert.Null(target.MetadataReferences);
            Assert.Null(target.ProjectContext);
            Assert.Null(target.Resources);
        }

        [Fact]
        public void PropertyCompilationIsSettable()
        {
            var compilation = CSharpCompilation.Create("nothing");
            var target = new BeforeCompileContext();

            target.Compilation = compilation;
            Assert.Equal(compilation, target.Compilation);
        }

        [Fact]
        public void ConstructorWithParameters()
        {
            var compilation = CSharpCompilation.Create("nothing");
            var projectContext = new ProjectContext();
            var resourceDescriptorList = new List<ResourceDescriptor>();
            var diagnosticList = new List<Diagnostic>();
            var metadataReferenceList = new List<IMetadataReference>();
            var target = new BeforeCompileContext(
                compilation,
                projectContext,
                () =>
                {
                    return resourceDescriptorList;
                },
                () =>
                {
                    return diagnosticList;
                },
                () =>
                {
                   return metadataReferenceList;
                });

            Assert.Equal(compilation, target.Compilation);
            Assert.Equal(projectContext, target.ProjectContext);
            Assert.Equal(resourceDescriptorList, target.Resources);
            Assert.Equal(diagnosticList, target.Diagnostics);
            Assert.Equal(metadataReferenceList, target.MetadataReferences);
        }
    }
}
