// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.Runtime.Json
{
    internal class JsonPrimitive : JsonValue
    {
        public JsonPrimitive(JsonPosition position) : base(position) { }
    }

    internal class JsonNull : JsonPrimitive
    {
        public JsonNull(JsonPosition position) : base(position) { }
    }

    /// Though Json doesn't distinguish between float and integer, JsonInteger
    /// is nice to have from the perspective of easy data consuming.
    internal class JsonInteger : JsonPrimitive
    {
        public JsonInteger(int value, JsonPosition position)
            : base(position)
        {
            Value = value;
        }

        public int Value { get; private set; }

        public static implicit operator int (JsonInteger jsonInteger)
        {
            return jsonInteger.Value;
        }
    }

    internal class JsonLong : JsonPrimitive
    {
        public JsonLong(long value, JsonPosition position)
            : base(position)
        {
            Value = value;
        }

        public long Value { get; private set; }

        public static implicit operator long (JsonLong jsonLong)
        {
            return jsonLong.Value;
        }
    }

    internal class JsonDecimal : JsonPrimitive
    {
        public JsonDecimal(decimal value, JsonPosition position)
            : base(position)
        {
            Value = value;
        }

        public decimal Value { get; private set; }

        public static implicit operator decimal (JsonDecimal jsonDecimal)
        {
            return jsonDecimal.Value;
        }
    }

    internal class JsonDouble : JsonPrimitive
    {
        public JsonDouble(double value, JsonPosition position)
            : base(position)
        {
            Value = value;
        }

        public double Value { get; private set; }

        public static implicit operator double (JsonDouble jsonDouble)
        {
            return jsonDouble.Value;
        }
    }

    internal class JsonBoolean : JsonPrimitive
    {
        public JsonBoolean(bool value, JsonPosition position)
            : base(position)
        {
            Value = value;
        }

        public bool Value { get; private set; }

        public static implicit operator bool (JsonBoolean jsonBoolean)
        {
            return jsonBoolean.Value;
        }
    }
}
