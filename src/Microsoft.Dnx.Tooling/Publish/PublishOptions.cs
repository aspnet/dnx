// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Dnx.Tooling.Publish
{
    public class PublishOptions
    {
        public string OutputDir { get; set; }

        public string ProjectDir { get; set; }

        public string Configuration { get; set; }

        public string WwwRoot { get; set; }

        public string WwwRootOut { get; set; }

        public FrameworkName RuntimeTargetFramework { get; set; }

        public bool NoSource { get; set; }

        public IList<string> Runtimes { get; set; }

        public bool Native { get; set; }

        public bool IncludeSymbols { get; set; }

        public Reports Reports { get; set; }
    }
}