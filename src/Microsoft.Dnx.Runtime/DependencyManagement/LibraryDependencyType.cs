// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Dnx.Runtime
{
    public class LibraryDependencyType
    {
        private readonly LibraryDependencyTypeFlag _flags;

        public static LibraryDependencyType Default = Parse("default");

        private LibraryDependencyType(LibraryDependencyTypeFlag flags)
        {
            _flags = flags;
        }

        public static LibraryDependencyType Parse(string keyword)
        {
            if (string.Equals(keyword, "default", StringComparison.OrdinalIgnoreCase))
            {
                return new LibraryDependencyType(
                       LibraryDependencyTypeFlag.MainReference |
                       LibraryDependencyTypeFlag.MainSource |
                       LibraryDependencyTypeFlag.MainExport |
                       LibraryDependencyTypeFlag.RuntimeComponent |
                       LibraryDependencyTypeFlag.BecomesNupkgDependency);
            }

            if (string.Equals(keyword, "build", StringComparison.OrdinalIgnoreCase))
            {
                return new LibraryDependencyType(
                    LibraryDependencyTypeFlag.MainSource |
                    LibraryDependencyTypeFlag.PreprocessComponent);
            }

            throw new InvalidOperationException(string.Format("unknown keyword {0}", keyword));
        }

        public bool Contains(LibraryDependencyTypeFlag flag)
        {
            return (_flags & flag) != 0;
        }
    }
}
