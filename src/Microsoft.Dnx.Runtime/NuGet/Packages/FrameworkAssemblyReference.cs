// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace NuGet
{
    public class FrameworkAssemblyReference : IFrameworkTargetable
    {
        public FrameworkAssemblyReference(string assemblyName, IEnumerable<FrameworkName> supportedFrameworks)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                throw new ArgumentException(nameof(assemblyName));
            }

            if (supportedFrameworks == null)
            {
                throw new ArgumentNullException(nameof(supportedFrameworks));
            }

            AssemblyName = assemblyName;
            SupportedFrameworks = supportedFrameworks;
        }

        public string AssemblyName { get; private set; }

        public IEnumerable<FrameworkName> SupportedFrameworks { get; private set; }
    }
}
