// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Dnx.Runtime
{
    public class LibraryDependencyTypeFlag
    {
        private static ConcurrentDictionary<string, LibraryDependencyTypeFlag> _flags = new ConcurrentDictionary<string, LibraryDependencyTypeFlag>();
        private readonly string _value;

        public static LibraryDependencyTypeFlag MainReference;
        public static LibraryDependencyTypeFlag MainSource;
        public static LibraryDependencyTypeFlag MainExport;
        public static LibraryDependencyTypeFlag PreprocessReference;

        public static LibraryDependencyTypeFlag RuntimeComponent;
        public static LibraryDependencyTypeFlag DevComponent;
        public static LibraryDependencyTypeFlag PreprocessComponent;
        public static LibraryDependencyTypeFlag BecomesNupkgDependency;

        static LibraryDependencyTypeFlag()
        {
            foreach (var fieldInfo in typeof(LibraryDependencyTypeFlag).GetTypeInfo().DeclaredFields)
            {
                if (fieldInfo.FieldType == typeof(LibraryDependencyTypeFlag))
                {
                    fieldInfo.SetValue(null, Declare(fieldInfo.Name));
                }
            }
        }

        LibraryDependencyTypeFlag(string value)
        {
            _value = value;
        }

        public static LibraryDependencyTypeFlag Declare(string keyword)
        {
            return _flags.GetOrAdd(keyword, x => new LibraryDependencyTypeFlag(x));
        }

        public override string ToString()
        {
            return _value;
        }
    }
}
