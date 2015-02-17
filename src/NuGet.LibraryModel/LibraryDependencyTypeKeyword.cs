// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.LibraryModel
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

            DeclareOnOff("MainReference", LibraryDependencyTypeFlag.MainReference, emptyFlags);
            DeclareOnOff("MainSource", LibraryDependencyTypeFlag.MainSource, emptyFlags);
            DeclareOnOff("MainExport", LibraryDependencyTypeFlag.MainExport, emptyFlags);
            DeclareOnOff("PreprocessReference", LibraryDependencyTypeFlag.PreprocessReference, emptyFlags);

            DeclareOnOff("RuntimeComponent", LibraryDependencyTypeFlag.RuntimeComponent, emptyFlags);
            DeclareOnOff("DevComponent", LibraryDependencyTypeFlag.DevComponent, emptyFlags);
            DeclareOnOff("PreprocessComponent", LibraryDependencyTypeFlag.PreprocessComponent, emptyFlags);
            DeclareOnOff("BecomesNupkgDependency", LibraryDependencyTypeFlag.BecomesNupkgDependency, emptyFlags);
        }

        private static void DeclareOnOff(string name, LibraryDependencyTypeFlag flag, IEnumerable<LibraryDependencyTypeFlag> emptyFlags)
        {
            Declare(name,
                    flagsToAdd: new[] { flag },
                    flagsToRemove: emptyFlags);

            Declare(
                name + "-off",
                flagsToAdd: emptyFlags,
                flagsToRemove: new[] { flag });
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
