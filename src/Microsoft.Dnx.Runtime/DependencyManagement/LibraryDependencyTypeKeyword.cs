// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Dnx.Runtime
{
    public class LibraryDependencyTypeKeyword
    {
        private static ConcurrentDictionary<string, LibraryDependencyTypeKeyword> _keywords = new ConcurrentDictionary<string, LibraryDependencyTypeKeyword>();

        public static LibraryDependencyTypeKeyword Default;
        public static LibraryDependencyTypeKeyword Build;
        public static LibraryDependencyTypeKeyword Preprocess;
        public static LibraryDependencyTypeKeyword Private;
        public static LibraryDependencyTypeKeyword Dev;

        private readonly string _value;
        private readonly IEnumerable<LibraryDependencyTypeFlag> _flagsToAdd;
        private readonly IEnumerable<LibraryDependencyTypeFlag> _flagsToRemove;

        public IEnumerable<LibraryDependencyTypeFlag> FlagsToAdd
        {
            get { return _flagsToAdd; }
        }

        public IEnumerable<LibraryDependencyTypeFlag> FlagsToRemove
        {
            get { return _flagsToRemove; }
        }

        static LibraryDependencyTypeKeyword()
        {
            var emptyFlags = Enumerable.Empty<LibraryDependencyTypeFlag>();

            Default = Declare(
                "default",
                flagsToAdd: new[]
                {
                    LibraryDependencyTypeFlag.MainReference,
                    LibraryDependencyTypeFlag.MainSource,
                    LibraryDependencyTypeFlag.MainExport,
                    LibraryDependencyTypeFlag.RuntimeComponent,
                    LibraryDependencyTypeFlag.BecomesNupkgDependency,
                },
                flagsToRemove: emptyFlags);

            Private = Declare(
                "private",
                flagsToAdd: new[]
                {
                    LibraryDependencyTypeFlag.MainReference,
                    LibraryDependencyTypeFlag.MainSource,
                    LibraryDependencyTypeFlag.RuntimeComponent,
                    LibraryDependencyTypeFlag.BecomesNupkgDependency,
                },
                flagsToRemove: emptyFlags);

            Dev = Declare(
                "dev",
                flagsToAdd: new[]
                {
                    LibraryDependencyTypeFlag.DevComponent,
                },
                flagsToRemove: emptyFlags);

            Build = Declare(
                "build",
                flagsToAdd: new[]
                {
                    LibraryDependencyTypeFlag.MainSource,
                    LibraryDependencyTypeFlag.PreprocessComponent,
                },
                flagsToRemove: emptyFlags);

            Preprocess = Declare(
                "preprocess",
                flagsToAdd: new[]
                {
                    LibraryDependencyTypeFlag.PreprocessReference,
                },
                flagsToRemove: emptyFlags);

            foreach (var fieldInfo in typeof(LibraryDependencyTypeFlag).GetTypeInfo().DeclaredFields)
            {
                if (fieldInfo.FieldType == typeof(LibraryDependencyTypeFlag))
                {
                    var flag = (LibraryDependencyTypeFlag)fieldInfo.GetValue(null);
                    Declare(
                        fieldInfo.Name,
                        flagsToAdd: new[] { flag },
                        flagsToRemove: emptyFlags);
                    Declare(
                        fieldInfo.Name + "-off",
                        flagsToAdd: emptyFlags,
                        flagsToRemove: new[] { flag });
                }
            }
        }

        private LibraryDependencyTypeKeyword(
            string value, 
            IEnumerable<LibraryDependencyTypeFlag> flagsToAdd, 
            IEnumerable<LibraryDependencyTypeFlag> flagsToRemove)
        {
            _value = value;
            _flagsToAdd = flagsToAdd;
            _flagsToRemove = flagsToRemove;
        }

        internal static LibraryDependencyTypeKeyword Declare(
            string keyword,
            IEnumerable<LibraryDependencyTypeFlag> flagsToAdd,
            IEnumerable<LibraryDependencyTypeFlag> flagsToRemove)
        {
            return _keywords.GetOrAdd(keyword, _ => new LibraryDependencyTypeKeyword(keyword, flagsToAdd, flagsToRemove));
        }

        internal static LibraryDependencyTypeKeyword Parse(string keyword)
        {
            LibraryDependencyTypeKeyword value;
            if (_keywords.TryGetValue(keyword, out value))
            {
                return value;
            }
            throw new Exception(string.Format("TODO: unknown keyword {0}", keyword));
        }
    }
}
