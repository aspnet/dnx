// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;

namespace NuGet.LibraryModel
{
    public class LibraryDependencyTypeFlag
    {
        private static ConcurrentDictionary<string, LibraryDependencyTypeFlag> _flags = new ConcurrentDictionary<string, LibraryDependencyTypeFlag>();
        private readonly string _value;

        public static LibraryDependencyTypeFlag MainReference = Declare("MainReference");
        public static LibraryDependencyTypeFlag MainSource = Declare("MainSource");
        public static LibraryDependencyTypeFlag MainExport = Declare("MainExport");
        public static LibraryDependencyTypeFlag PreprocessReference = Declare("PreprocessReference");

        public static LibraryDependencyTypeFlag RuntimeComponent = Declare("RuntimeComponent");
        public static LibraryDependencyTypeFlag DevComponent = Declare("DevComponent");
        public static LibraryDependencyTypeFlag PreprocessComponent = Declare("PreprocessComponent");
        public static LibraryDependencyTypeFlag BecomesNupkgDependency = Declare("BecomesNupkgDependency");

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
    }
}
