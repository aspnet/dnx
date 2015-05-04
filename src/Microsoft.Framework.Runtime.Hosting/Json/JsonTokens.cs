// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.Runtime.Json
{
    internal class JsonToken
    {
        public JsonToken(string value, int line, int column)
        {
            String = value;
            Position = new JsonPosition(line, column);
        }

        public string String { get; private set; }

        public JsonPosition Position { get; private set; }

        public static implicit operator string(JsonToken token)
        {
            return token.String;
        }
    }
}