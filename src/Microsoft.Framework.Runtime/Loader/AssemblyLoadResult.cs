// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Framework.Runtime.Loader
{
    // REVIEW: Should this be a struct?
    public class AssemblyLoadResult
    {
        public Assembly Assembly { get; private set; }

        public Exception Error { get; private set; }

        public AssemblyLoadResult()
        {
        }

        public AssemblyLoadResult(Exception error)
        {
            Error = error;
        }

        public AssemblyLoadResult(Assembly assembly)
        {
            Assembly = assembly;
        }
    }
}
