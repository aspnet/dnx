// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Security.AccessControl;
using Microsoft.AspNet.Testing.xunit;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class ServicingBreadcrumbsFacts
    {
        private readonly string TempFolderPath = Path.Combine(Path.GetTempPath(), "breadcrumbs_test");

        public ServicingBreadcrumbsFacts()
        {
            if (Directory.Exists(TempFolderPath))
            {
                Directory.Delete(TempFolderPath, recursive: true);
            }

            Directory.CreateDirectory(TempFolderPath);
        }

        [Fact]
        public void BreadcrumbsAreCreatedSuccessfully()
        {
            Assert.Empty(Directory.GetFiles(TempFolderPath));

            var breadcrumbs = new Servicing.Breadcrumbs(TempFolderPath);
            breadcrumbs.AddBreadcrumb("Test", new NuGet.SemanticVersion("1.0.0"));
            breadcrumbs.WriteAllBreadcrumbs();

            Assert.True(File.Exists(Path.Combine(TempFolderPath, "Test.1.0.0")));
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        public void BreadcrumbsCreationDoesNotFailWhenAccessDenied()
        {
            var userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

            var directory = new DirectoryInfo(TempFolderPath);
            var security = new DirectorySecurity();
            security.AddAccessRule(new FileSystemAccessRule(userName, FileSystemRights.Write, AccessControlType.Deny));
            directory.SetAccessControl(security);

            var breadcrumbs = new Servicing.Breadcrumbs(TempFolderPath);
            breadcrumbs.AddBreadcrumb("Test", new NuGet.SemanticVersion("1.0.0"));
            breadcrumbs.WriteAllBreadcrumbs();

            Assert.Empty(Directory.GetFiles(TempFolderPath));
        }

        [Fact]
        public void BreadcrumbsFolderNotCreatedIfDoesntExist()
        {
            var nonExistingBreadcrumbsFolder = Path.Combine(Path.GetTempPath(), "fake_breadcrumbs");
            if (Directory.Exists(nonExistingBreadcrumbsFolder))
            {
                Directory.Delete(nonExistingBreadcrumbsFolder);
            }

            var breadcrumbs = new Servicing.Breadcrumbs(nonExistingBreadcrumbsFolder);
            breadcrumbs.AddBreadcrumb("Test", new NuGet.SemanticVersion("1.0.0"));
            breadcrumbs.WriteAllBreadcrumbs();

            Assert.False(Directory.Exists(nonExistingBreadcrumbsFolder));
        }

        [Fact]
        public void EnqueueDoesNotWriteToDisk()
        {
            Assert.Empty(Directory.GetFiles(TempFolderPath));

            var breadcrumbs = new Servicing.Breadcrumbs(TempFolderPath);
            breadcrumbs.AddBreadcrumb("Test", new NuGet.SemanticVersion("1.0.0"));

            Assert.False(File.Exists(Path.Combine(TempFolderPath, "Test.1.0.0")));
        }
    }
}