// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;

namespace NuGet
{
    public class NetPortableProfileCollection : KeyedCollection<string, NetPortableProfile>
    {
        public NetPortableProfileCollection()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        protected override string GetKeyForItem(NetPortableProfile item)
        {
            return item.Name;
        }
    }
}