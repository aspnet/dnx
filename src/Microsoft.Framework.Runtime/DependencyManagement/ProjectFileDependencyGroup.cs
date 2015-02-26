// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Framework.Runtime.DependencyManagement
{
    public class ProjectFileDependencyGroup
    {
        public ProjectFileDependencyGroup(string frameworkName, IEnumerable<string> dependencies)
        {
            FrameworkName = frameworkName;
            Dependencies = dependencies;
        }

        public string FrameworkName { get; }

        public IEnumerable<string> Dependencies { get; }
    }
}