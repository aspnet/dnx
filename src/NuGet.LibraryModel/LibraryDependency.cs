// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;

namespace NuGet.LibraryModel
{
    public class LibraryDependency
    {
        public LibraryRange Range { get; }

        public LibraryDependencyType Type { get; } = LibraryDependencyType.Default;

        public LibraryIdentity ResolvedIdentity { get; set; }

        public string Name
        {
            get
            {
                return Range.Name;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Range);
            sb.Append(" ");

            if (ResolvedIdentity != null)
            {
                sb.Append("(" + ResolvedIdentity + ")");
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
