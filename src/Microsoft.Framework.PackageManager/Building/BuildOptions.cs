// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Framework.PackageManager
{
    public class BuildOptions
    {
        public string OutputDir { get; set; }

        public string ProjectDir { get; set; }

        public IList<string> Configurations { get; set; }

        public IList<string> TargetFrameworks { get;set; }

        public Reports Reports { get; set; }

        public BuildOptions()
        {
            Configurations = new List<string>();
            TargetFrameworks = new List<string>();
        }
    }
}
