// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Runtime.Json
{
    internal struct JsonToken
    {
        public JsonTokenType Type;
        public string Value;
        public int Line;
        public int Column;
    }
}
