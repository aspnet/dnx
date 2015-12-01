// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Extensions.CompilationAbstractions
{
    /// <summary>
    /// Provides an interface to well-known Compiler Options like "defines" and "optimize", as well as a
    /// general-purpose interface for reading from the 'compilerOptions' section.
    /// </summary>
    public interface ICompilerOptions
    {
        IEnumerable<string> Defines { get; }

        string LanguageVersion { get; }

        string Platform { get; }

        bool? AllowUnsafe { get; }

        bool? WarningsAsErrors { get; }

        bool? Optimize { get; }

        string KeyFile { get; }

        bool? DelaySign { get; }

        bool? UseOssSigning { get; }

        bool? EmitEntryPoint { get; }
    }
}