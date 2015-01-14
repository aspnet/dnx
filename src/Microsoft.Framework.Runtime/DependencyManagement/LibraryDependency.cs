// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class LibraryDependency
    {
        public LibraryRange LibraryRange { get; set; }

        public LibraryDependencyType Type { get; set; }

        public Library Library { get; set; }

        public string Name
        {
            get
            {
                return LibraryRange.Name;
            }
        }

        public override string ToString()
        {
            return (Library?.ToString() ?? LibraryRange.ToString()) + " " + Type;
        }

        public bool HasFlag(LibraryDependencyTypeFlag flag)
        {
            return Type.Contains(flag);
        }
    }
}
