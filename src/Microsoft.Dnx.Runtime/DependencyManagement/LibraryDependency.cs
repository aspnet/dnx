// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class LibraryDependency
    {
        public LibraryRange LibraryRange { get; set; }

        public LibraryDependencyType Type { get; set; } = LibraryDependencyType.Default;

        public LibraryIdentity Library { get; set; }

        public string Name
        {
            get
            {
                return LibraryRange.Name;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(LibraryRange);
            sb.Append(" ");

            if (Library != null)
            {
                sb.Append("(" + Library + ")");
                sb.Append(" ");
            }

            sb.Append(Type);
            return sb.ToString();
        }

        public bool HasFlag(LibraryDependencyTypeFlag flag)
        {
            return Type.Contains(flag);
        }
    }
}
