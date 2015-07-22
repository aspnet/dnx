// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime.Infrastructure;

namespace Microsoft.Dnx.Runtime
{
    /// <summary>
    /// Provides the identity of a specific target for a compilation.
    /// </summary>
    public struct CompilationTarget : IEquatable<CompilationTarget>
    {
        public CompilationTarget(string name, FrameworkName targetFramework, string configuration, string aspect)
        {
            Name = name;
            TargetFramework = targetFramework;
            Configuration = configuration;
            Aspect = aspect;
        }

        public string Name { get; }
        public FrameworkName TargetFramework { get; }
        public string Configuration { get; }
        public string Aspect { get; }

        public bool Equals(CompilationTarget other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                string.Equals(Configuration, other.Configuration, StringComparison.Ordinal) &&
                string.Equals(Aspect, other.Aspect, StringComparison.Ordinal) &&
                TargetFramework.Equals(other.TargetFramework);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(Name)
                .Add(TargetFramework)
                .Add(Configuration)
                .Add(Aspect)
                .CombinedHash;
        }
    }
}
