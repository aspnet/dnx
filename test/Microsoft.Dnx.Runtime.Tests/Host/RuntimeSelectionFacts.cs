using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.CompilationAbstractions;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests.Host
{
    public class RuntimeSelectionFacts
    {
        private static readonly FrameworkName Dnx451 = new FrameworkName(VersionUtility.DnxFrameworkIdentifier, new Version(4, 5, 1));

        [Fact]
        public void ApplicationHostContext_SelectsSingleMatchingLockFileSectionIfPresent()
        {
            // Arrange
            var ahc = new ApplicationHostContext()
            {
                Project = CreateDummyProject(),
                TargetFramework = Dnx451,
                RuntimeIdentifiers = new[] { "win7-x64" },
                LockFile = new LockFile()
                {
                    Version = Constants.LockFileVersion,
                    Targets = new List<LockFileTarget>()
                    {
                        new LockFileTarget()
                        {
                            TargetFramework = Dnx451,
                            RuntimeIdentifier = string.Empty,
                            Libraries = new List<LockFileTargetLibrary>()
                            {
                                new LockFileTargetLibrary()
                                {
                                    Name = "NoRuntime",
                                    Version = new SemanticVersion(1, 0, 0, 0)
                                }
                            }
                        },
                        new LockFileTarget()
                        {
                            TargetFramework = Dnx451,
                            RuntimeIdentifier = "win7-x64",
                            Libraries = new List<LockFileTargetLibrary>()
                            {
                                new LockFileTargetLibrary()
                                {
                                    Name = "Runtime.win7-x64",
                                    Version = new SemanticVersion(1, 0, 0, 0)
                                }
                            }
                        }
                    },
                    PackageLibraries = new List<LockFilePackageLibrary>()
                    {
                        new LockFilePackageLibrary()
                        {
                            Name = "NoRuntime",
                            Version = new SemanticVersion(1, 0, 0, 0)
                        },
                        new LockFilePackageLibrary()
                        {
                            Name ="Runtime.win7-x64",
                            Version = new SemanticVersion(1, 0, 0, 0)
                        }
                    }
                }
            };

            // Act
            var libs = ApplicationHostContext.GetRuntimeLibraries(ahc);

            // Assert
            Assert.Equal(new[] { "DummyProject/1.0.0", "Runtime.win7-x64/1.0.0" }, libs.Select(l => $"{l.Identity.Name}/{l.Identity.Version}").ToArray());
        }

        [Fact]
        public void ApplicationHostContext_SelectsRuntimeIndependentTargetIfNoRuntimeIdsProvided()
        {
            // Arrange
            var ahc = new ApplicationHostContext()
            {
                Project = CreateDummyProject(),
                TargetFramework = Dnx451,
                LockFile = new LockFile()
                {
                    Version = Constants.LockFileVersion,
                    Targets = new List<LockFileTarget>()
                    {
                        new LockFileTarget()
                        {
                            TargetFramework = Dnx451,
                            RuntimeIdentifier = string.Empty,
                            Libraries = new List<LockFileTargetLibrary>()
                            {
                                new LockFileTargetLibrary()
                                {
                                    Name = "NoRuntime",
                                    Version = new SemanticVersion(1, 0, 0, 0)
                                }
                            }
                        },
                        new LockFileTarget()
                        {
                            TargetFramework = Dnx451,
                            RuntimeIdentifier = "win7-x64",
                            Libraries = new List<LockFileTargetLibrary>()
                            {
                                new LockFileTargetLibrary()
                                {
                                    Name = "Runtime.win7-x64",
                                    Version = new SemanticVersion(1, 0, 0, 0)
                                }
                            }
                        }
                    },
                    PackageLibraries = new List<LockFilePackageLibrary>()
                    {
                        new LockFilePackageLibrary()
                        {
                            Name = "NoRuntime",
                            Version = new SemanticVersion(1, 0, 0, 0)
                        },
                        new LockFilePackageLibrary()
                        {
                            Name = "Runtime.win7-x64",
                            Version = new SemanticVersion(1, 0, 0, 0)
                        }
                    }
                }
            };

            // Act
            var libs = ApplicationHostContext.GetRuntimeLibraries(ahc);

            // Assert
            Assert.Equal(new[] { "DummyProject/1.0.0", "NoRuntime/1.0.0" }, libs.Select(l => $"{l.Identity.Name}/{l.Identity.Version}").ToArray());
        }

        [Fact]
        public void ApplicationHostContext_PerformsFallbackThroughRuntimeIdentifiers()
        {
            // Arrange
            var ahc = new ApplicationHostContext()
            {
                Project = CreateDummyProject(),
                TargetFramework = Dnx451,
                RuntimeIdentifiers = new[] { "win10-x64", "win8-x64", "win7-x64" },
                LockFile = new LockFile()
                {
                    Version = Constants.LockFileVersion,
                    Targets = new List<LockFileTarget>()
                    {
                        new LockFileTarget()
                        {
                            TargetFramework = Dnx451,
                            RuntimeIdentifier = string.Empty,
                            Libraries = new List<LockFileTargetLibrary>()
                            {
                                new LockFileTargetLibrary()
                                {
                                    Name = "NoRuntime",
                                    Version = new SemanticVersion(1, 0, 0, 0)
                                }
                            }
                        },
                        new LockFileTarget()
                        {
                            TargetFramework = Dnx451,
                            RuntimeIdentifier = "win7-x64",
                            Libraries = new List<LockFileTargetLibrary>()
                            {
                                new LockFileTargetLibrary()
                                {
                                    Name = "Runtime.win7-x64",
                                    Version = new SemanticVersion(1, 0, 0, 0)
                                }
                            }
                        },
                        new LockFileTarget()
                        {
                            TargetFramework = Dnx451,
                            RuntimeIdentifier = "win8-x64",
                            Libraries = new List<LockFileTargetLibrary>()
                            {
                                new LockFileTargetLibrary()
                                {
                                    Name = "Runtime.win8-x64",
                                    Version = new SemanticVersion(1, 0, 0, 0)
                                }
                            }
                        }

                    },
                    PackageLibraries = new List<LockFilePackageLibrary>()
                    {
                        new LockFilePackageLibrary()
                        {
                            Name = "NoRuntime",
                            Version = new SemanticVersion(1, 0, 0, 0)
                        },
                        new LockFilePackageLibrary()
                        {
                            Name = "Runtime.win7-x64",
                            Version = new SemanticVersion(1, 0, 0, 0)
                        },
                        new LockFilePackageLibrary()
                        {
                            Name = "Runtime.win8-x64",
                            Version = new SemanticVersion(1, 0, 0, 0)
                        }
                    }
                }
            };

            // Act
            var libs = ApplicationHostContext.GetRuntimeLibraries(ahc);

            // Assert
            Assert.Equal(new[] { "DummyProject/1.0.0", "Runtime.win8-x64/1.0.0" }, libs.Select(l => $"{l.Identity.Name}/{l.Identity.Version}").ToArray());
        }

        private Project CreateDummyProject()
        {
            const string DummyProject = @"{ ""frameworks"": { ""dnx451"": {} } }";
            using (var strm = new MemoryStream(Encoding.UTF8.GetBytes(DummyProject)))
            {
                return new ProjectReader().ReadProject(strm, "DummyProject", @"C:\DummyProject", new List<Microsoft.Extensions.CompilationAbstractions.DiagnosticMessage>());
            }
        }
    }
}
