// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Security.AccessControl;
using Microsoft.AspNet.Testing.xunit;
using Xunit;

namespace Microsoft.Framework.Runtime.Tests
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

        [ConditionalFact]
        [OSSkipCondition(OperatingSystems.MacOSX | OperatingSystems.Unix)]
        public void RuntimeBreadcrumbIsCreated()
        {
            var breadcrumbs = new Servicing.Breadcrumbs(TempFolderPath);
            breadcrumbs.CreateRuntimeBreadcrumb();

            var runtimeBreadcrumbFile = typeof(Servicing.Breadcrumbs).Assembly.GetName().Name;
            Assert.True(File.Exists(Path.Combine(TempFolderPath, runtimeBreadcrumbFile)));
        }

        [ConditionalFact]
        [OSSkipCondition(OperatingSystems.MacOSX | OperatingSystems.Unix)]
        public void BreadcrumbsAreCreatedSuccessfully()
        {
            Assert.Empty(Directory.GetFiles(TempFolderPath));

            var breadcrumbs = new Servicing.Breadcrumbs(TempFolderPath);
            breadcrumbs.LeaveBreadcrumb("Test", new NuGet.SemanticVersion("1.0.0"));

            Assert.True(File.Exists(Path.Combine(TempFolderPath, "Test")));
            Assert.True(File.Exists(Path.Combine(TempFolderPath, "Test.1.0.0")));
        }

        [ConditionalFact]
        [OSSkipCondition(OperatingSystems.MacOSX | OperatingSystems.Unix)]
        public void BreadcrumbsCreationDoesNotFailWhenAccessDenied()
        {
            var userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

            var directory = new DirectoryInfo(TempFolderPath);
            var security = new DirectorySecurity();
            security.AddAccessRule(new FileSystemAccessRule(userName, FileSystemRights.Write, AccessControlType.Deny));
            directory.SetAccessControl(security);

            var breadcrumbs = new Servicing.Breadcrumbs(TempFolderPath);
            breadcrumbs.LeaveBreadcrumb("Test", new NuGet.SemanticVersion("1.0.0"));

            Assert.Empty(Directory.GetFiles(TempFolderPath));
        }

        [ConditionalFact]
        [OSSkipCondition(OperatingSystems.MacOSX | OperatingSystems.Unix)]
        public void BreadcrumbsFolderNotCreatedIfDoesntExist()
        {
            var nonExistingBreadcrumbsFolder = Path.Combine(Path.GetTempPath(), "fake_breadcrumbs");
            if (Directory.Exists(nonExistingBreadcrumbsFolder))
            {
                Directory.Delete(nonExistingBreadcrumbsFolder);
            }

            var breadcrumbs = new Servicing.Breadcrumbs(nonExistingBreadcrumbsFolder);
            breadcrumbs.LeaveBreadcrumb("Test", new NuGet.SemanticVersion("1.0.0"));

            Assert.False(Directory.Exists(nonExistingBreadcrumbsFolder));
        }
    }
}