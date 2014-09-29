// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Framework.Runtime
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
        private readonly IEnumerable<LibraryDependencyTypeFlag> _add;
        private readonly IEnumerable<LibraryDependencyTypeFlag> _remove;

        public IEnumerable<LibraryDependencyTypeFlag> Add
        {
            get { return _add; }
        }

        public IEnumerable<LibraryDependencyTypeFlag> Remove
        {
            get { return _remove; }
        }

        static LibraryDependencyTypeKeyword()
        {
            Default = Declare(
                "default",
                add: Group(
                    LibraryDependencyTypeFlag.MainReference,
                    LibraryDependencyTypeFlag.MainSource,
                    LibraryDependencyTypeFlag.MainExport,
                    LibraryDependencyTypeFlag.RuntimeComponent,
                    LibraryDependencyTypeFlag.BecomesNupkgDependency),
                remove: Group(
                    ));

            Private = Declare(
                "private",
                add: Group(
                    LibraryDependencyTypeFlag.MainReference,
                    LibraryDependencyTypeFlag.MainSource,
                    LibraryDependencyTypeFlag.RuntimeComponent,
                    LibraryDependencyTypeFlag.BecomesNupkgDependency),
                remove: Group());

            Dev = Declare(
                "dev",
                add: Group(
                    LibraryDependencyTypeFlag.DevComponent),
                remove: Group());

            Build = Declare(
                "build",
                add: Group(
                    LibraryDependencyTypeFlag.MainSource,
                    LibraryDependencyTypeFlag.PreprocessComponent),
                remove: Group());

            Preprocess = Declare(
                "preproc",
                add: Group(
                    LibraryDependencyTypeFlag.PreprocessReference),
                remove: Group());

            foreach (var fieldInfo in typeof(LibraryDependencyTypeFlag).GetTypeInfo().DeclaredFields)
            {
                if (fieldInfo.FieldType == typeof(LibraryDependencyTypeFlag))
                {
                    var flag = (LibraryDependencyTypeFlag)fieldInfo.GetValue(null);
                    Declare(
                        fieldInfo.Name,
                        Group(flag),
                        Group());
                    Declare(
                        fieldInfo.Name + "-off",
                        Group(),
                        Group(flag));
                }
            }
        }

        LibraryDependencyTypeKeyword(string value, IEnumerable<LibraryDependencyTypeFlag> add, IEnumerable<LibraryDependencyTypeFlag> remove)
        {
            _value = value;
            _add = add;
            _remove = remove;
        }

        public static IEnumerable<LibraryDependencyTypeFlag> Group(params LibraryDependencyTypeFlag[] flags)
        {
            return flags;
        }

        public static LibraryDependencyTypeKeyword Declare(
            string keyword,
            IEnumerable<LibraryDependencyTypeFlag> add,
            IEnumerable<LibraryDependencyTypeFlag> remove)
        {
            return _keywords.GetOrAdd(keyword, x => new LibraryDependencyTypeKeyword(x, add, remove));
        }

        internal static LibraryDependencyTypeKeyword Parse(string keyword)
        {
            if (_keywords.TryGetValue(keyword, out var value))
            {
                return value;
            }
            throw new Exception(string.Format("TODO: unknown keyword {0}", keyword));
        }
    }
}
