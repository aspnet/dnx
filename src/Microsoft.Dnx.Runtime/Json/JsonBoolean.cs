// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Dnx.Runtime.Json
{
    internal class JsonBoolean : JsonValue
    {
        public JsonBoolean(JsonToken token)
            : base(token.Line, token.Column)
        {
            if (token.Type == JsonTokenType.True)
            {
                Value = true;
            }
            else if (token.Type == JsonTokenType.False)
            {
                Value = false;
            }
            else
            {
                throw new ArgumentException("Token value should be either True or False.", nameof(token));
            }
        }

        public bool Value { get; private set; }

        public static implicit operator bool (JsonBoolean jsonBoolean)
        {
            return jsonBoolean.Value;
        }
    }
}
