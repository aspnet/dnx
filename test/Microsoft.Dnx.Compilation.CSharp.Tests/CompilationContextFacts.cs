// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    public class CompilationContextFacts
    {
        [Fact]
        public void DefaultConstructorInitializeCorrectly()
        {
            var compilation = CSharpCompilation.Create("fakecompilation");
            var fakeProject = CreateMockProject();

            var resourceResolverInvoked = false;

            var context = new CompilationContext(
                compilation,
                fakeProject,
                new List<IMetadataReference>(),
                () =>
                {
                    resourceResolverInvoked = true;
                    return new List<ResourceDescriptor>();
                });

            Assert.NotNull(context.Modules);
            Assert.Equal(0, context.Modules.Count);
            Assert.Equal(compilation, context.Compilation);
            Assert.Equal(compilation, context.BeforeCompileContext.Compilation);
            Assert.Equal(fakeProject, context.Project);
            Assert.Equal(fakeProject.Target.TargetFramework, context.ProjectContext.TargetFramework);
            Assert.Equal(fakeProject.Target.Configuration, context.ProjectContext.Configuration);

            Assert.False(resourceResolverInvoked, "Resource resolver should not be invoked");
            Assert.Equal(0, context.Resources.Count);
            Assert.True(resourceResolverInvoked);

            var newCompilation = compilation.Clone();
            context.Compilation = newCompilation;

            Assert.NotEqual(compilation, context.Compilation);
            Assert.NotEqual(compilation, context.BeforeCompileContext.Compilation);
            Assert.Equal(newCompilation, context.Compilation);
            Assert.Equal(newCompilation, context.BeforeCompileContext.Compilation);
        }

        private static CompilationProjectContext CreateMockProject()
        {
            return new CompilationProjectContext(
                new CompilationTarget("MockProject", new FrameworkName(".NETFramework, Version=8.0"), "fakeConfiguration", null),
                "c:\\wonderland",
                "c:\\wonderland\\project.json",
                "Title",
                "Description",
                "Copyright",
                "0.0.1-rc-fake",
                new Version(0, 0, 1),
                embedInteropTypes: false,
                files: null,
                compilerOptions: null);
        }
    }
}
