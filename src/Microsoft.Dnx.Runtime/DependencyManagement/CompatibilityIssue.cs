// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class CompatibilityIssue
    {
        public string LibraryName { get; set; }
        public SemanticVersion LibraryVersion { get; set; }
        public FrameworkName Framework{ get; set; }
        public IssueType Type { get; set; }

        public enum IssueType
        {
            Unknown = 0,
            UnsupportedFramework,
            MissingRuntimeAssembly
        }

        public override string ToString()
        {
            switch (Type)
            {
                case IssueType.UnsupportedFramework:
                    return $"{LibraryName} {LibraryVersion} doesn't support {Framework}.";
                case IssueType.MissingRuntimeAssembly:
                    return $"{LibraryName} {LibraryVersion} provides a compile-time reference assembly for {Framework}, but there is no compatible run-time assembly.";
                default:
                    return $"Unknown compatibility issue was found with {LibraryName} {LibraryVersion} for {Framework}.";
            }
        }
    }
}
