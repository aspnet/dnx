// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;

namespace Microsoft.Dnx.Host
{
    public class BootstrapperContext
    {
        public string OperatingSystem { get; set; }

        public string OsVersion { get; set; }

        public string Architecture { get; set; }

        public string RuntimeDirectory { get; set; }

        public string ApplicationBase { get; set; }

        public FrameworkName TargetFramework { get; set; }

        public bool HandleExceptions { get; set; }
    }
}
