// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Framework.Runtime.Json;
using Xunit;

namespace Microsoft.Framework.Runtime.Tests
{
    public class JsonDeserializerFacts
    {
        [Fact]
        public void JsonIntegerImplicitConversion()
        {
            var json = new JsonInteger(123, new JsonPosition(1, 321));
            int value = json;

            Assert.Equal(123, value);
            Assert.NotNull(json.Position);
            Assert.Equal(1, json.Position.Line);
            Assert.Equal(321, json.Position.Column);
        }

        [Fact]
        public void DeserialzeEmptyString()
        {
            var target = new JsonDeserializer();

            var result = target.Deserialize(string.Empty);

            Assert.Null(result);
        }

        [Fact]
        public void DeserialzeIntegerArray()
        {
            var target = new JsonDeserializer();

            var raw = target.Deserialize("[1,2,3]");
            Assert.NotNull(raw);

            var list = raw as JsonArray;
            Assert.NotNull(list);
            Assert.Equal(3, list.Count);

            for (int i = 0; i < 3; ++i)
            {
                var integer = list[i] as JsonInteger;
                Assert.NotNull(integer);
                Assert.NotNull(list[i].Position);
                Assert.Equal(0, list[i].Position.Line);
                Assert.Equal(1 + 2 * i, list[i].Position.Column);
                Assert.Equal(i + 1, integer.Value);
                Assert.Equal(i + 1, integer); // impliict conversion
            }

            // test list cast
            var values = list.Cast<JsonInteger>();
            Assert.Equal(1, values[0]);
            Assert.Equal(2, values[1]);
            Assert.Equal(3, values[2]);
        }

        [Fact]
        public void DeserializeStringArray()
        {
            var target = new JsonDeserializer();

            var raw = target.Deserialize(@"[""a"", ""b"", ""c"" ]");
            Assert.NotNull(raw);

            var list = raw as JsonArray;

            Assert.NotNull(list);
            Assert.Equal(3, list.Count);
            Assert.NotNull(list.Position);
            Assert.Equal(0, list.Position.Line);
            Assert.Equal(0, list.Position.Column);

            for (int i = 0; i < 3; ++i)
            {
                Assert.NotNull(list[i].Position);
                Assert.Equal(0, list[i].Position.Line);
                Assert.Equal(1 + 5 * i, list[i].Position.Column);

                var jstring = list[i] as JsonString;
                Assert.NotNull(jstring);
            }

            Assert.Equal("a", list[0].ToString());
            Assert.Equal("b", list[1].ToString());
            Assert.Equal("c", list[2].ToString());
        }

        [Fact]
        public void DeserializeSimpleObject()
        {
            var target = new JsonDeserializer();

            // Do not format the following 12 lines. The position of every charactor position in the
            // json sample is referenced in following test.
            var raw = target.Deserialize(@"
            {
                ""key1"": ""value1"",
                ""key2"": 99,
                ""key3"": true,
                ""key4"": [""str1"", ""str2"", ""str3""],
                ""key5"": {
                    ""subkey1"": ""subvalue1"",
                    ""subkey2"": [1, 2]
                },
                ""key6"": null
            }");

            Assert.NotNull(raw);

            var jobject = raw as JsonObject;
            Assert.NotNull(jobject);
            Assert.Equal("value1", jobject.ValueAsString("key1"));
            Assert.Equal(99, (JsonInteger)jobject.Value("key2"));
            Assert.Equal(true, jobject.ValueAsBoolean("key3"));
            Assert.NotNull(jobject.Position);
            Assert.Equal(1, jobject.Position.Line);
            Assert.Equal(12, jobject.Position.Column);

            var list = jobject.ValueAsStringArray("key4");
            Assert.NotNull(list);
            Assert.Equal(3, list.Length);
            Assert.Equal("str1", list[0]);
            Assert.Equal("str2", list[1]);
            Assert.Equal("str3", list[2]);

            var rawList = jobject.Value("key4") as JsonArray;
            Assert.NotNull(rawList);
            Assert.NotNull(rawList.Position);
            Assert.Equal(5, rawList.Position.Line);
            Assert.Equal(24, rawList.Position.Column);

            var subObject = jobject.ValueAsJsonObject("key5");
            Assert.NotNull(subObject);
            Assert.Equal("subvalue1", subObject.ValueAsString("subkey1"));

            var subArray = subObject.ValueAsJsonArray("subkey2");
            Assert.NotNull(subArray);
            Assert.Equal(2, subArray.Count);
            Assert.Equal(1, (JsonInteger)subArray[0]);
            Assert.Equal(2, (JsonInteger)subArray[1]);
            Assert.NotNull(subArray.Position);
            Assert.Equal(8, subArray.Position.Line);
            Assert.Equal(31, subArray.Position.Column);

            var nullValue = jobject.Value("key6");
            Assert.NotNull(nullValue);
            Assert.True(nullValue is JsonNull);
        }

        [Fact]
        public void DeserializeLockFile()
        {
            using (var fs = File.OpenRead(".\\TestSample\\project.lock.sample"))
            {
                var deserializer = new JsonDeserializer();
                var raw = deserializer.Deserialize(fs);

                Assert.NotNull(raw);
                Assert.True(raw is JsonObject);
            }
        }
    }
}
