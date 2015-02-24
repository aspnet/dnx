// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Framework.Runtime.Hosting;

namespace Microsoft.Framework.Runtime.FunctionalTests.ProjectFileGlobbing
{
    public class ProjectFileGlobbingFacts : ProjectFilesCollectionTests
    {
        protected override ProjectFilesCollection CreateFilesCollection(string jsonContent, string projectDir)
        {
            var project = Project.GetProject(
                jsonContent,
                "testproject",
                Path.Combine(_context.RootPath, projectDir, "project.json"));

            return project.Files;
        }
    }
}
