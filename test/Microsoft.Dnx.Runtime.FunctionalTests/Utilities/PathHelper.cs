// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Dnx.Runtime.FunctionalTests.Utilities
{
    public static class PathHelper
    {
        public static string NormalizeSeparator(string path)
        {
            return path.Replace('\\', Path.DirectorySeparatorChar)
                       .Replace('/', Path.DirectorySeparatorChar);
        }
    }
}