// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;

namespace NuGet.LibraryModel
{
    public class LibraryDependencyTypeFlag : IEquatable<LibraryDependencyTypeFlag>
    {
        private static ConcurrentDictionary<string, LibraryDependencyTypeFlag> _flags = new ConcurrentDictionary<string, LibraryDependencyTypeFlag>();
        private readonly string _value;

        public static readonly LibraryDependencyTypeFlag MainReference = Declare("MainReference");
        public static readonly LibraryDependencyTypeFlag MainSource = Declare("MainSource");
        public static readonly LibraryDependencyTypeFlag MainExport = Declare("MainExport");
        public static readonly LibraryDependencyTypeFlag PreprocessReference = Declare("PreprocessReference");

        public static readonly LibraryDependencyTypeFlag RuntimeComponent = Declare("RuntimeComponent");
        public static readonly LibraryDependencyTypeFlag DevComponent = Declare("DevComponent");
        public static readonly LibraryDependencyTypeFlag PreprocessComponent = Declare("PreprocessComponent");
        public static readonly LibraryDependencyTypeFlag BecomesNupkgDependency = Declare("BecomesNupkgDependency");

        private LibraryDependencyTypeFlag(string value)
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

        public bool Equals(LibraryDependencyTypeFlag other)
        {
            return other != null && string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LibraryDependencyTypeFlag);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }
    }
}
