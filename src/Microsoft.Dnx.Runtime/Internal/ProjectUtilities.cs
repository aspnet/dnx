// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.CompilationAbstractions;

namespace Microsoft.Dnx.Runtime.Internal
{
    public class ProjectUtilities
    {
        /// <summary>
        /// Create project from a project.json in string
        /// </summary>
        public static Project GetProject(string json,
                                         string projectName,
                                         string projectPath,
                                         ICollection<DiagnosticMessage> diagnostics = null)
        {
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var project = new ProjectReader().ReadProject(ms, projectName, projectPath, diagnostics);

            return project;
        }
    }
}
