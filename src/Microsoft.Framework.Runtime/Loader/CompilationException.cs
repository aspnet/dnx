// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Summary description for RoslynCompileException
    /// </summary>
    public class CompilationException : Exception
    {
        public CompilationException(IList<string> errors)
        {
            Errors = errors;
        }

        public IList<string> Errors { get; private set; }
    }
}