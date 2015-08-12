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

        public string Message
        {
            get
            {
                var frameworkShortName = VersionUtility.GetShortFrameworkName(Framework);
                var frameworkFullName = $"{Framework} ({frameworkShortName})";
                var libraryFullName = $"{LibraryName} {LibraryVersion}";

                switch (Type)
                {
                    case IssueType.UnsupportedFramework:
                        return $"{libraryFullName} doesn't support {frameworkFullName}.";
                    case IssueType.MissingRuntimeAssembly:
                        return $"{libraryFullName} is incompatible with {frameworkFullName}. It provides a compile-time reference assembly but there is no compatible run-time assembly.";
                    default:
                        return $"Unknown compatibility issue was found with {libraryFullName} for {frameworkFullName}.";
                }
            }
        }

        public enum IssueType
        {
            Unknown = 0,
            UnsupportedFramework,
            MissingRuntimeAssembly
        }
    }
}
