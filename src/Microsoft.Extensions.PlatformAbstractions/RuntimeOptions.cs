// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime
{
    /// <summary>
    /// Represents the options passed into the runtime on boot
    /// </summary>
    public class RuntimeOptions
    {
        public string ApplicationName { get; set; }

        public string ApplicationBaseDirectory { get; set; }

        public FrameworkName TargetFramework { get; set; }

        public string Configuration { get; set; }

        public int? CompilationServerPort { get; set; }
    }
}