// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Dnx.Runtime
{
    public class LibraryDependencyType
    {
        private readonly LibraryDependencyTypeFlag[] _keywords;

        public static LibraryDependencyType Default;

        static LibraryDependencyType()
        {
            Default = Parse(new[] { "default" });
        }

        public LibraryDependencyType()
        {
            _keywords = new LibraryDependencyTypeFlag[0];
        }

        private LibraryDependencyType(LibraryDependencyTypeFlag[] flags)
        {
            _keywords = flags;
        }

        public bool Contains(LibraryDependencyTypeFlag flag)
        {
            return _keywords.Contains(flag);
        }

        public static LibraryDependencyType Parse(IEnumerable<string> keywords)
        {
            var type = new LibraryDependencyType();
            foreach (var keyword in keywords.Select(LibraryDependencyTypeKeyword.Parse))
            {
                type = type.Combine(keyword.FlagsToAdd, keyword.FlagsToRemove);
            }
            return type;
        }

        public LibraryDependencyType Combine(
            IEnumerable<LibraryDependencyTypeFlag> add,
            IEnumerable<LibraryDependencyTypeFlag> remove)
        {
            return new LibraryDependencyType(
                _keywords.Except(remove).Union(add).ToArray());
        }

        public override string ToString()
        {
            return string.Join(",", _keywords.Select(kw => kw.ToString()));
        }
    }
}
