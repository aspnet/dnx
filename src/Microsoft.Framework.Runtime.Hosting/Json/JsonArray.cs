// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Framework.Runtime.Json
{
    internal class JsonArray : JsonValue
    {
        private readonly JsonValue[] _array;

        public JsonArray(JsonValue[] array, int line, int column)
            : base(line, column)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            _array = array;
        }

        public int Length
        {
            get { return _array.Length; }
        }

        public JsonValue this[int index]
        {
            get { return _array[index]; }
        }
    }
}
