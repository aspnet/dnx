// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Microsoft.Dnx.Testing
{
    public static class TestConstants
    {
        public static readonly string TestOutputDirectory = Path.Combine("artifacts", "TestOut");
        public static readonly string TestSolutionsDirectory = "misc";
        public static readonly string SaveFilesAll = "all";
        public static readonly string SaveFilesNone = "none";
        public static readonly ReadOnlyDictionary<string, string> RuntimeAcronyms = 
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>()
                {
                    { "clr", "c" },
                    { "coreclr", "cc" },
                    { "mono", "m" },
                    { "win", "w" },
                    { "darwin", "d" },
                    { "linux", "l" },
                    { "x64", "6" },
                    { "x86", "8" },
                    { "arm", "a" },
                });
    }
}
