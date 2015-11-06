// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Versioning;
using Microsoft.Extensions.Internal;

namespace Microsoft.Extensions.CompilationAbstractions
{
    /// <summary>
    /// Provides the identity of a specific target for a compilation.
    /// </summary>
    public struct CompilationTarget : IEquatable<CompilationTarget>
    {
        // TODO(anurse): This can probably be reduced to remove the name field since we always seem to have the name already ;)
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
            var hashCodeCombiner = HashCodeCombiner.Start();
            hashCodeCombiner.Add(Name);
            hashCodeCombiner.Add(TargetFramework);
            hashCodeCombiner.Add(Configuration);
            hashCodeCombiner.Add(Aspect);

            return hashCodeCombiner;
        }

        public override string ToString()
        {
            string aspectString = string.IsNullOrEmpty(Aspect) ? string.Empty : $"!{Aspect}";
            return $"{Name}{aspectString} ({TargetFramework}, {Configuration})";
        }
    }
}
