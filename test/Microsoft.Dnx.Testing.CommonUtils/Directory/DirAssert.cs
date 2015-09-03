// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Testing
{
    public class DirAssert
    {
        public static void Equal(Dir expected, Dir actual)
        {
            var diff = actual.Diff(expected);
            if (diff.NoDiff)
            {
                return;
            }
            throw new DirMismatchException(expected.LoadPath, actual.LoadPath, diff);
        }
    }
}
