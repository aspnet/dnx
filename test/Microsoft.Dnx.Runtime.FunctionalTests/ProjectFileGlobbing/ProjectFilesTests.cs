// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Dnx.Runtime.FunctionalTests.Utilities;

namespace Microsoft.Dnx.Runtime.FunctionalTests.ProjectFileGlobbing
{
    public class ProjectFilesTests : ProjectFilesCollectionTests
    {
        protected override ProjectFilesCollection CreateFilesCollection(string jsonContent, string projectDir)
        {
            var project = Project.GetProject(
                jsonContent,
                "testproject",
                Path.Combine(Root.DirPath, PathHelper.NormalizeSeparator(projectDir), "project.json"));

            return project.Files;
        }
    }
}
