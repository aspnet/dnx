// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface ICompilerOptions
    {
        IEnumerable<string> Defines { get; }

        string LanguageVersion { get; }

        string Platform { get; }

        bool? AllowUnsafe { get; }

        bool? WarningsAsErrors { get; }

        bool? Optimize { get; }
    }
}