// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Framework.Runtime.FunctionalTests.Utilities;

namespace Microsoft.Framework.Runtime.FunctionalTests.ProjectFileGlobbing
{
    public class ProjectFileTests : ProjectFilesCollectionTests
    {
        protected override IProjectFilesCollection CreateFilesCollection(string jsonContent, string projectDir)
        {
            var project = Project.GetProject(
                jsonContent,
                "testproject",
                Path.Combine(_context.RootPath, PathHelper.NormalizeSeparator(projectDir), "project.json"));

            return project.Files;
        }
    }
}
