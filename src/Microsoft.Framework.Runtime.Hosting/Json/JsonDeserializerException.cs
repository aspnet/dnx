// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Framework.Runtime.Json
{
    internal class JsonDeserializerException : Exception
    {
        public JsonDeserializerException(string message, int currentLine, int currentColumn)
            : base(message)
        {
            Line = currentLine;
            Column = currentColumn;
        }

        public int Line { get; private set; }

        public int Column { get; private set; }
    }
}
