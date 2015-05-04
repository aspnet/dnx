// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Framework.Runtime.Json
{
    internal class JsonArray : JsonValue
    {
        private readonly JsonValue[] _array;

        public JsonArray(JsonValue[] array, JsonPosition position)
            : base(position)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            _array = array;
        }

        public int Count
        {
            get { return _array.Length; }
        }

        public JsonValue this[int index]
        {
            get { return _array[index]; }
        }
    }
}
