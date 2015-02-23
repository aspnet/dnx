// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;
namespace Microsoft.Framework.ApplicationHost
{
    public class ApplicationHostOptions
    {
        public string ApplicationName { get; set; }
        public string ApplicationBaseDirectory { get; set; }
        public string PackageDirectory { get; set; }
        public FrameworkName TargetFramework { get; set; }
        public string Configuration { get; set; }
        public bool WatchFiles { get; set; }
        public int? CompilationServerPort { get; set; }
    }
}
