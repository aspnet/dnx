// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    public class AfterCompileContextFacts
    {
        [Fact]
        public void DefaultConstructorSetAllPropertiesNull()
        {
            var target = new AfterCompileContext();
            
            // nothing is set
            Assert.Null(target.Compilation);
            Assert.Null(target.Diagnostics);
            Assert.Null(target.ProjectContext);
            Assert.Null(target.AssemblyStream);
            Assert.Null(target.SymbolStream);
            Assert.Null(target.XmlDocStream);
        }

        [Fact]
        public void PropertyCompilationIsSettable()
        {
            var target = new AfterCompileContext();
            var compilation = CSharpCompilation.Create("nothing");

            target.Compilation = compilation;
            Assert.Equal(compilation, target.Compilation);
        }
    }
}
