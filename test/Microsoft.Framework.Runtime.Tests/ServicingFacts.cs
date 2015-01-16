// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.AccessControl;
using Xunit;

namespace Microsoft.Framework.Runtime.Tests
{
    public class ServicingFacts
    {
        private readonly string TempFolderPath = Path.Combine(Path.GetTempPath(), "breadcrumbs_test");

        public ServicingFacts()
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

            Servicing.ServicingBreadcrumbs breadcrumbs = new Servicing.ServicingBreadcrumbs(TempFolderPath);
            breadcrumbs.LeaveBreadcrumb("Test", new NuGet.SemanticVersion("1.0.0"));

            Assert.True(File.Exists(Path.Combine(TempFolderPath, "Test")));
            Assert.True(File.Exists(Path.Combine(TempFolderPath, "Test.1.0.0")));
        }

        [Fact]
        public void BreadcrumbsCreationDoesNotFailWhenAccessDenied()
        {
            string userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

            DirectoryInfo directory = new DirectoryInfo(TempFolderPath);
            DirectorySecurity security = new DirectorySecurity();
            security.AddAccessRule(new FileSystemAccessRule(userName, FileSystemRights.Write, AccessControlType.Deny));
            directory.SetAccessControl(security);

            Servicing.ServicingBreadcrumbs breadcrumbs = new Servicing.ServicingBreadcrumbs(TempFolderPath);
            breadcrumbs.LeaveBreadcrumb("Test", new NuGet.SemanticVersion("1.0.0"));

            Assert.Empty(Directory.GetFiles(TempFolderPath));
        }

        [Fact]
        public void BreadcrumbsFolderNotCreatedIfDoesntExist()
        {
            string nonExistingBreadcrumbsFolder = Path.Combine(Path.GetTempPath(), "fake_breadcrumbs");
            if (Directory.Exists(nonExistingBreadcrumbsFolder))
            {
                Directory.Delete(nonExistingBreadcrumbsFolder);
            }

            Servicing.ServicingBreadcrumbs breadcrumbs = new Servicing.ServicingBreadcrumbs(nonExistingBreadcrumbsFolder);
            breadcrumbs.LeaveBreadcrumb("Test", new NuGet.SemanticVersion("1.0.0"));

            Assert.False(Directory.Exists(nonExistingBreadcrumbsFolder));
        }
    }
}