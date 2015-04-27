// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Framework.Runtime.Json
{
    internal class JsonObject : JsonValue
    {
        private readonly IDictionary<string, JsonValue> _data;

        public JsonObject(IDictionary<string, JsonValue> data, JsonPosition position)
            : base(position)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            _data = data;
        }

        public ICollection<string> Keys
        {
            get { return _data.Keys; }
        }

        public JsonValue Value(string key)
        {
            JsonValue result;
            if (!_data.TryGetValue(key, out result))
            {
                result = null;
            }

            return result;
        }

        public T ValueAs<T>(string key, Func<JsonValue, T> cast)
        {
            return cast(Value(key));
        }

        public JsonObject ValueAsJsonObject(string key)
        {
            return ValueAs<JsonObject>(key, value =>
            {
                return value as JsonObject;
            });
        }

        public JsonString ValueAsString(string key)
        {
            return ValueAs(key, value => value as JsonString);
        }

        public bool ValueAsBoolean(string key, bool defaultValue = false)
        {
            return ValueAs(key, value =>
            {
                if (value != null && value is JsonBoolean)
                {
                    return (value as JsonBoolean).Value;
                }

                return defaultValue;
            });
        }

        public bool? ValueAsNullableBoolean(string key)
        {
            return ValueAs<bool?>(key, value =>
            {
                if (value != null && value is JsonBoolean)
                {
                    return (value as JsonBoolean).Value;
                }
                else
                {
                    return null;
                }
            });
        }

        public JsonArray ValueAsJsonArray(string key)
        {
            return ValueAs(key, value => value as JsonArray);
        }

        public string[] ValueAsStringArray(string key)
        {
            return ValueAs(key, value =>
            {
                var list = value as JsonArray;
                if (list == null)
                {
                    return null;
                }

                var result = new string[list.Count];

                for (int i = 0; i < list.Count; ++i)
                {
                    var jsonString = list[i] as JsonString;
                    result[i] = jsonString?.ToString();
                }

                return result;
            });
        }
    }
}
