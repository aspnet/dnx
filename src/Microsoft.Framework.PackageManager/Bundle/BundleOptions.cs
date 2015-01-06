// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Framework.PackageManager.Packing
{
    public class BundleOptions
    {
        public string OutputDir { get; set; }

        public string ProjectDir { get; set; }

        public string Configuration { get; set; }

        public string WwwRoot { get; set; }

        public string WwwRootOut { get; set; }

        public FrameworkName RuntimeTargetFramework { get; set; }

        public bool Overwrite { get; set; }

        public bool NoSource { get; set; }

        public IEnumerable<string> Runtimes { get; set; }

        public bool Native { get; set; }

        public Reports Reports { get; set; }
    }
}