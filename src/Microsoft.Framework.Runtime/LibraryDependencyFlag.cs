using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Framework.Runtime
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
                type = type.Combine(keyword.Add, keyword.Remove);
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
                    LibraryDependencyTypeFlag.MainExport,
                    LibraryDependencyTypeFlag.RuntimeComponent,
                    LibraryDependencyTypeFlag.BecomesNupkgDependency),
                remove: Group(
                    ));

            Private = Declare(
                "private",
                add: Group(
                    LibraryDependencyTypeFlag.MainReference,
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
                    LibraryDependencyTypeFlag.PreprocessComponent
                    ),
                remove: Group());

            Preprocess = Declare(
                "preproc",
                add: Group(
                    LibraryDependencyTypeFlag.PreprocessReference
                    ),
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

    public class LibraryDependencyTypeFlag
    {
        private static ConcurrentDictionary<string, LibraryDependencyTypeFlag> _flags = new ConcurrentDictionary<string, LibraryDependencyTypeFlag>();
        private readonly string _value;

        public static LibraryDependencyTypeFlag MainReference;
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
