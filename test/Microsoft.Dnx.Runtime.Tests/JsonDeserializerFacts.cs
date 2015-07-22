// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Dnx.Runtime.Json;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class JsonDeserializerFacts
    {
        [Theory]
        [InlineData("123")]
        public void JsonNumberToInt(string raw)
        {
            var token = new JsonToken
            {
                Value = raw,
                Type = JsonTokenType.Number,
                Line = 1,
                Column = 321
            };

            var json = new JsonNumber(token);
            int value = FromJsonNumberToInt(json);

            Assert.Equal(123, value);
            Assert.Equal(1, json.Line);
            Assert.Equal(321, json.Column);
        }

        [Fact]
        public void DeserialzeEmptyString()
        {
            using (var reader = GetReader(string.Empty))
            {
                var result = JsonDeserializer.Deserialize(reader);
                Assert.Null(result);
            }
        }

        [Fact]
        public void DeserializeEmptyArray()
        {
            using (var reader = GetReader("[]"))
            {
                var result = JsonDeserializer.Deserialize(reader) as JsonArray;
                Assert.NotNull(result);
                Assert.Equal(0, result.Length);
            }
        }

        [Fact]
        public void DeserialzeIntegerArray()
        {
            using (var reader = GetReader("[1,2,3]"))
            {
                var raw = JsonDeserializer.Deserialize(reader);
                Assert.NotNull(raw);

                var list = raw as JsonArray;
                Assert.NotNull(list);
                Assert.Equal(3, list.Length);

                for (int i = 0; i < 3; ++i)
                {
                    Assert.Equal(1, list[i].Line);
                    Assert.Equal(2 + 2 * i, list[i].Column);
                    Assert.Equal(i + 1, FromJsonNumberToInt(list[i]));
                }
            }
        }

        [Fact]
        public void DeserializeStringArray()
        {
            using (var reader = GetReader(@"[""a"", ""b"", ""c"" ]"))
            {
                var raw = JsonDeserializer.Deserialize(reader);
                Assert.NotNull(raw);

                var list = raw as JsonArray;

                Assert.NotNull(list);
                Assert.Equal(3, list.Length);
                Assert.Equal(1, list.Line);
                Assert.Equal(1, list.Column);

                for (int i = 0; i < 3; ++i)
                {
                    Assert.Equal(1, list[i].Line);
                    Assert.Equal(2 + 5 * i, list[i].Column);

                    var jstring = list[i] as JsonString;
                    Assert.NotNull(jstring);
                }

                Assert.Equal("a", list[0].ToString());
                Assert.Equal("b", list[1].ToString());
                Assert.Equal("c", list[2].ToString());
            }
        }

        [Fact]
        public void DeserializeSimpleObject()
        {
            // Do not format the following 12 lines. The position of every charactor position in the
            // JSON sample is referenced in following test.
            var content = @"
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
            }";

            using (var reader = GetReader(content))
            {
                var raw = JsonDeserializer.Deserialize(reader);

                Assert.NotNull(raw);

                var jobject = raw as JsonObject;
                Assert.NotNull(jobject);
                Assert.Equal("value1", jobject.ValueAsString("key1"));
                Assert.Equal(99, FromJsonNumberToInt(jobject.Value("key2")));
                Assert.Equal(true, jobject.ValueAsBoolean("key3"));
                Assert.Equal(2, jobject.Line);
                Assert.Equal(13, jobject.Column);

                var list = jobject.ValueAsStringArray("key4");
                Assert.NotNull(list);
                Assert.Equal(3, list.Length);
                Assert.Equal("str1", list[0]);
                Assert.Equal("str2", list[1]);
                Assert.Equal("str3", list[2]);

                var rawList = jobject.Value("key4") as JsonArray;
                Assert.NotNull(rawList);
                Assert.NotNull(rawList);
                Assert.Equal(6, rawList.Line);
                Assert.Equal(25, rawList.Column);

                var subObject = jobject.ValueAsJsonObject("key5");
                Assert.NotNull(subObject);
                Assert.Equal("subvalue1", subObject.ValueAsString("subkey1"));

                var subArray = subObject.Value("subkey2") as JsonArray;
                Assert.NotNull(subArray);
                Assert.Equal(2, subArray.Length);
                Assert.Equal(1, FromJsonNumberToInt(subArray[0]));
                Assert.Equal(2, FromJsonNumberToInt(subArray[1]));
                Assert.Equal(9, subArray.Line);
                Assert.Equal(32, subArray.Column);

                var nullValue = jobject.Value("key6");
                Assert.NotNull(nullValue);
                Assert.True(nullValue is JsonNull);
            }
        }

        [Theory]
        [InlineData("{[}")]
        [InlineData("{[}]")]
        [InlineData("{}}")]
        [InlineData("{{}")]
        [InlineData("[[]")]
        [InlineData("[]]")]
        [InlineData("{\"}")]
        [InlineData("}")]
        [InlineData("]")]
        [InlineData("nosuchthing")]
        public void DeserializeIncorrectJSON(string content)
        {
            using (var reader = GetReader(content))
            {
                Assert.Throws<JsonDeserializerException>(() =>
                {
                    JsonDeserializer.Deserialize(reader);
                });
            }
        }

        [Fact]
        public void DeserializeLockFile()
        {
            using (var fs = File.OpenRead(Path.Combine("TestSample", "project.lock.sample")))
            {
                var reader = new StreamReader(fs);
                var raw = JsonDeserializer.Deserialize(reader);

                Assert.NotNull(raw);
                Assert.True(raw is JsonObject);
            }
        }

        private int FromJsonNumberToInt(JsonValue jsonValue)
        {
            var number = jsonValue as JsonNumber;
            Assert.NotNull(number);

            return Convert.ToInt32(number.Raw);
        }

        private TextReader GetReader(string content)
        {
            return new StringReader(content);
        }
    }
}
