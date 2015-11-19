// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class TargetFrameworkInformation : IFrameworkTargetable
    {
        public FrameworkName FrameworkName { get; set; }

        public IReadOnlyList<LibraryDependency> Dependencies { get; set; }

        public string WrappedProject { get; set; }

        public string AssemblyPath { get; set; }

        public string PdbPath { get; set; }

        public IEnumerable<FrameworkName> SupportedFrameworks
        {
            get
            {
                return new[] { FrameworkName };
            }
        }
    }
}