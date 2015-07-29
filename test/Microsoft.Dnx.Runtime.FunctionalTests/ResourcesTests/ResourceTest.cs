// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation.CSharp;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Runtime.Infrastructure;
using Microsoft.Dnx.Runtime.Internal;
using Xunit;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using Microsoft.Dnx.Runtime.Loader;
using System.Reflection;
using Microsoft.Dnx.Host;
using System.Globalization;

namespace Microsoft.Dnx.Runtime.FunctionalTests.ResourcesTests
{
    public class ResourceTest
    {
        [Fact]
        public void ReadResourcesAssembly()
        {
            //System.Diagnostics.Debugger.Launch();
            var testProjectFolder = Path.Combine(
                TestUtils.GetMiscProjectsFolder(), "ResourcesTestProjects/ReadFromResources/src/ReadFromResources");

            var projectContext = new CompilationProjectContext(
                new CompilationTarget("ResourcesProject", new FrameworkName(".NETFramework, Version=4.5"), "Debug", null),
                testProjectFolder,
                Path.Combine(testProjectFolder, "project.json"),
                "1.0.0",
                new Version(0, 0, 1),
                embedInteropTypes: false,
                files: null,
                compilerOptions: null);

            Project project;
            bool projectFound = Project.TryGetProject(testProjectFolder, out project);
            Assert.True(projectFound);

            var compilationContext = new CompilationContext(
                CSharpCompilation.Create("ResourceCompilation"),
                projectContext,
                new List<IMetadataReference>(),
                () => CompositeResourceProvider.Default.GetResources(project)
            );

            //var compilationContext = XCreateCompilationContext();

            var projectReference = new RoslynProjectReference(compilationContext);
            var container = new LoaderContainer();
            var lc = new DefaultLoadContext(container);
            var assembly = lc.LoadAssembly(new AssemblyName("ReadFromResources.Resources"));

            var myAssemblyName = new AssemblyName();
            myAssemblyName.CultureInfo = new CultureInfo("fr-FR");
            myAssemblyName.Name = "HelloWorld.Resources";
            var myAssembly = projectReference.Load(myAssemblyName, lc);
            Assert.NotNull(myAssembly);


        }

        public CompilationContext XCreateCompilationContext()
        {
            var testProjectFolder = Path.Combine(
                TestUtils.GetMiscProjectsFolder(), "ResourcesTestProjects/ReadFromResources/src/ReadFromResources");

            var projectContext = new CompilationProjectContext(
                new CompilationTarget("ResourcesProject", new FrameworkName(".NETFramework, Version=4.5"), "Debug", null),
                testProjectFolder,
                Path.Combine(testProjectFolder,"project.json"),
                "1.0.0",
                new Version(0, 0, 1),
                embedInteropTypes: false,
                files: null,
                compilerOptions: null);

            Project project;
            bool projectFound = Project.TryGetProject(testProjectFolder, out project);
            Assert.True(projectFound);

            return new CompilationContext(
                CSharpCompilation.Create("ResourceCompilation"),
                projectContext,
                new List<IMetadataReference>(),
                () => CompositeResourceProvider.Default.GetResources(project)
            );
        }
    }
}
