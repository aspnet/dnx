// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Framework.PackageManager.Crossgen
{
    public class CrossgenOptions
    {
        public string CrossgenPath { get; set; }

        public string RuntimePath { get; set; }

        public IEnumerable<string> InputPaths { get; set; }

        public bool Symbols { get; set; }

        public bool Partial { get; set; }
    }
}