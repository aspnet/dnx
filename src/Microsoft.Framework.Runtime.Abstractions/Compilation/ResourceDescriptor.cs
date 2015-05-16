// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Framework.Runtime.Compilation
{
    public class ResourceDescriptor
    {
        public string Name { get; set; }

        public string FileName { get; set; }

        public Func<Stream> StreamFactory { get; set; }
    }
}