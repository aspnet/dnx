// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class NuGetDependencyResolverFacts
    {
        [Theory]
        [InlineData("MetaPackage", ".NETFramework,Version=v4.5", true)]
        [InlineData("MetaPackage", "DNX,Version=v4.5.1", true)]
        [InlineData("Net451LibPackage", ".NETFramework,Version=v4.5", false)]
        [InlineData("Net451LibPackage", "DNX,Version=v4.5.1", true)]
        [InlineData("Net451RefPackage", ".NETFramework,Version=v4.5", false)]
        [InlineData("Net451RefPackage", "DNX,Version=v4.5.1", true)]
        [InlineData("AssemblyPlaceholderPackage", ".NETFramework,Version=v4.5", true)]
        [InlineData("AssemblyPlaceholderPackage", "DNX,Version=v4.5.1", false)]
        public void LibrariesAreResolvedCorrectly(string packageName, string framework, bool resolved)
        {
            var repo = new PackageRepository("path/to/packages");
            var resolver = new NuGetDependencyResolver(repo);

            var net45Target = new LockFileTarget
            {
                TargetFramework = new FrameworkName(".NETFramework,Version=v4.5"),
                Libraries = new List<LockFileTargetLibrary>
                {
                    new LockFileTargetLibrary
                    {
                        Name = "MetaPackage",
                        Version = SemanticVersion.Parse("1.0.0"),
                    },
                    new LockFileTargetLibrary
                    {
                        Name = "Net451LibPackage",
                        Version = SemanticVersion.Parse("1.0.0"),
                    },
                    new LockFileTargetLibrary
                    {
                        Name = "Net451RefPackage",
                        Version = SemanticVersion.Parse("1.0.0"),
                    },
                    new LockFileTargetLibrary
                    {
                        Name = "AssemblyPlaceholderPackage",
                        Version = SemanticVersion.Parse("1.0.0"),
                        CompileTimeAssemblies = new List<LockFileItem> { Path.Combine("ref", "net45", "_._") },
                        RuntimeAssemblies = new List<LockFileItem> { Path.Combine("lib", "net45", "_._") }
                    }
                }
            };

            var dnx451Target = new LockFileTarget
            {
                TargetFramework = new FrameworkName("DNX,Version=v4.5.1"),
                Libraries = new List<LockFileTargetLibrary>
                {
                    new LockFileTargetLibrary
                    {
                        Name = "MetaPackage",
                        Version = SemanticVersion.Parse("1.0.0")
                    },
                    new LockFileTargetLibrary
                    {
                        Name = "Net451LibPackage",
                        Version = SemanticVersion.Parse("1.0.0"),
                        CompileTimeAssemblies = new List<LockFileItem> { Path.Combine("lib", "net451", "Net451LibPackage.dll") },
                        RuntimeAssemblies = new List<LockFileItem> { Path.Combine("lib", "net451", "Net451LibPackage.dll") }
                    },
                    new LockFileTargetLibrary
                    {
                        Name = "Net451RefPackage",
                        Version = SemanticVersion.Parse("1.0.0"),
                        CompileTimeAssemblies = new List<LockFileItem> { Path.Combine("ref", "net451", "Net451LibPackage.dll") }
                    },
                    new LockFileTargetLibrary
                    {
                        Name = "AssemblyPlaceholderPackage",
                        Version = SemanticVersion.Parse("1.0.0")
                    }
                }
            };

            var metaPackageLibrary = new LockFilePackageLibrary
            {
                Name = "MetaPackage",
                Version = SemanticVersion.Parse("1.0.0")
            };

            var net451LibPackageLibrary = new LockFilePackageLibrary
            {
                Name = "Net451LibPackage",
                Version = SemanticVersion.Parse("1.0.0"),
                Files = new List<string>
                {
                    Path.Combine("lib", "net451", "Net451LibPackage.dll"),
                    Path.Combine("lib", "net451", "Net451LibPackage.xml")
                }
            };

            var net451RefPackageLibrary = new LockFilePackageLibrary
            {
                Name = "Net451RefPackage",
                Version = SemanticVersion.Parse("1.0.0"),
                Files = new List<string>
                {
                    Path.Combine("ref", "net451", "Net451LibPackage.dll")
                }
            };

            var assemblyPlaceholderPackageLibrary = new LockFilePackageLibrary
            {
                Name = "AssemblyPlaceholderPackage",
                Version = SemanticVersion.Parse("1.0.0"),
                Files = new List<string>
                {
                    Path.Combine("lib", "net45", "_._"),
                    Path.Combine("ref", "net45", "_._"),
                }
            };

            var lockFile = new LockFile()
            {
                Targets = new List<LockFileTarget> { net45Target, dnx451Target },
                PackageLibraries = new List<LockFilePackageLibrary>
                {
                    metaPackageLibrary,
                    net451LibPackageLibrary,
                    net451RefPackageLibrary,
                    assemblyPlaceholderPackageLibrary
                }
            };

            resolver.ApplyLockFile(lockFile);

            var libToLookup = new LibraryRange(packageName, frameworkReference: false);
            Assert.Equal(resolved, resolver.GetDescription(libToLookup, new FrameworkName(framework)).Resolved);
        }
    }
}