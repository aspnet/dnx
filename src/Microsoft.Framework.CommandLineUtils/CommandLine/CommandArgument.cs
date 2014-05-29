// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Framework.Runtime.Common.CommandLine
{
    internal class CommandArgument
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Value { get; set; }
    }
}
