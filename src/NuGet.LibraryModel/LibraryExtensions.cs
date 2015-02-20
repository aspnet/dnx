// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.LibraryModel
{
    public static class LibraryExtensions
    {
        public static T GetProperty<T>(this Library library, string key)
        {
            object value;
            if (library.Properties.TryGetValue(key, out value))
            {
                return (T)value;
            }
            return default(T);
        }
    }
}