// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.Runtime.Json
{
    internal class JsonPosition
    {
        public JsonPosition(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public int Line { get; private set; }

        public int Column { get; private set; }
    }
}
