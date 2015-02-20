// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.LibraryModel
{
    public static class LibraryRangeExtensions
    {
        public static bool IsEclipsedBy(this LibraryRange library, LibraryRange other)
        {
            return string.Equals(library.Name, other.Name, StringComparison.OrdinalIgnoreCase) && 
                   string.Equals(library.Type, other.Type);
        }
    }
}