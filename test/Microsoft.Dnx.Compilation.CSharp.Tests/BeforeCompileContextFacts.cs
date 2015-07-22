// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp;
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
    }
}
