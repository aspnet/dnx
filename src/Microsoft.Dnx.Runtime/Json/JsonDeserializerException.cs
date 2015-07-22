// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Dnx.Runtime.Json
{
    internal class JsonDeserializerException : Exception
    {
        public JsonDeserializerException(string message, Exception innerException, int line, int column)
            : base(message, innerException)
        {
            Line = line;
            Column = column;
        }

        public JsonDeserializerException(string message, int line, int column)
            : base(message)
        {
            Line = line;
            Column = column;
        }

        public JsonDeserializerException(string message, JsonToken nextToken)
            : base(message)
        {
            Line = nextToken.Line;
            Column = nextToken.Column;
        }

        public int Line { get; }

        public int Column { get; }
    }
}
