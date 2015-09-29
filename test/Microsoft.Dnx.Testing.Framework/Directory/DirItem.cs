// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Testing
{
    public class DirItem
    {
        public DirItem(object item, bool skipComparison)
        {
            Item = item;
            SkipComparison = skipComparison;
        }

        public DirItem(object item)
            : this(item, false)
        {
        }

        public DirItem()
            : this(Dir.EmptyFile, true)
        {
        }

        public bool SkipComparison { get; set; }

        public object Item { get; set; }
    }
}
