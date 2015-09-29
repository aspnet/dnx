// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Dnx.Testing
{
    public static class TestConstants
    {
        public static readonly string TestOutputDirectory = Path.Combine("artifacts", "TestOut");
        public static readonly string TestSolutionsDirectory = "misc";
        public static readonly string SaveFilesAll = "all";
        public static readonly string SaveFilesNone = "none";
    }
}
