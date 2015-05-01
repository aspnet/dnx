// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Framework.Runtime.Compilation;
using Xunit;

namespace Microsoft.Framework.Runtime.Roslyn.Tests
{
    public class CompilationContextFacts
    {
        [Fact]
        public void DefaultConstructorInitializeCorrectly()
        {
            var compilation = CSharpCompilation.Create("fakecompilation");
            var fakeProject = new MockCompilationProject();
            var fakeFramework = new FrameworkName(".NET Framework, Version=8.0");
            var fakeConfiguration = "fakeConfiguration";

            var resourceResolverInvoked = false;

            var context = new CompilationContext(
                compilation,
                fakeProject,
                fakeFramework,
                fakeConfiguration,
                new List<IMetadataReference>(),
                () =>
                {
                    resourceResolverInvoked = true;
                    return new List<CodeAnalysis.ResourceDescription>();
                });

            Assert.NotNull(context.Modules);
            Assert.Equal(0, context.Modules.Count);
            Assert.Equal(compilation, context.Compilation);
            Assert.Equal(compilation, context.BeforeCompileContext.Compilation);
            Assert.Equal(fakeProject, context.Project);
            Assert.Equal(fakeFramework, context.ProjectContext.TargetFramework);
            Assert.Equal(fakeConfiguration, context.ProjectContext.Configuration);

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

        private class MockCompilationProject : ICompilationProject
        {
            public string AssemblyFileVersion
            {
                get { throw new NotImplementedException(); }
            }

            public bool EmbedInteropTypes
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
            }

            public IProjectFilesCollection Files
            {
                get { throw new NotImplementedException(); }
            }

            public string Name
            {
                get { return "MockProject"; }
            }

            public string ProjectDirectory
            {
                get { return "c:\\wonderland"; }
            }

            public string ProjectFilePath
            {
                get { return "c:\\wonderland\\project.json"; }
            }

            public string Version
            {
                get { return "0.0.1-rc-fake"; }
            }

            public ICompilerOptions GetCompilerOptions(FrameworkName targetFramework, string configuration)
            {
                throw new NotImplementedException();
            }
        }
    }
}
