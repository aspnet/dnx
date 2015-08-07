// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Runtime.Json
{
    internal class JsonValue
    {
        public JsonValue(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public int Line { get; }

        public int Column { get; }
    }
}
