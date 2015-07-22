// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Dnx.Runtime.Json
{
    internal static class JsonDeserializer
    {
        public static JsonValue Deserialize(TextReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var buffer = new JsonBuffer(reader);

            var result = DeserializeInternal(buffer.Read(), buffer);

            // There are still unprocessed char. The parsing is not finished. Error happened.
            var nextToken = buffer.Read();
            if (nextToken.Type != JsonTokenType.EOF)
            {
                throw new JsonDeserializerException(
                    JsonDeserializerResource.Format_UnfinishedJSON(nextToken.Value),
                    nextToken);
            }

            return result;
        }

        private static JsonValue DeserializeInternal(JsonToken next, JsonBuffer buffer)
        {
            if (next.Type == JsonTokenType.EOF)
            {
                return null;
            }

            if (next.Type == JsonTokenType.LeftSquareBracket)
            {
                return DeserializeArray(next, buffer);
            }

            if (next.Type == JsonTokenType.LeftCurlyBracket)
            {
                return DeserializeObject(next, buffer);
            }

            if (next.Type == JsonTokenType.String)
            {
                return new JsonString(next.Value, next.Line, next.Column);
            }

            if (next.Type == JsonTokenType.True || next.Type == JsonTokenType.False)
            {
                return new JsonBoolean(next);
            }

            if (next.Type == JsonTokenType.Null)
            {
                return new JsonNull(next.Line, next.Column);
            }

            if (next.Type == JsonTokenType.Number)
            {
                return new JsonNumber(next);
            }

            throw new JsonDeserializerException(JsonDeserializerResource.Format_InvalidTokenExpectation(
                next.Value, "'{', '[', true, false, null, JSON string, JSON number, or the end of the file"), 
                next);
        }

        private static JsonArray DeserializeArray(JsonToken head, JsonBuffer buffer)
        {
            var list = new List<JsonValue>();
            while (true)
            {
                var next = buffer.Read();
                if (next.Type == JsonTokenType.RightSquareBracket)
                {
                    break;
                }

                list.Add(DeserializeInternal(next, buffer));

                next = buffer.Read();
                if (next.Type == JsonTokenType.EOF)
                {
                    throw new JsonDeserializerException(
                        JsonDeserializerResource.Format_InvalidSyntaxExpectation("JSON array", ']', ','),
                        next);
                }
                else if (next.Type == JsonTokenType.RightSquareBracket)
                {
                    break;
                }
                else if (next.Type != JsonTokenType.Comma)
                {
                    throw new JsonDeserializerException(
                        JsonDeserializerResource.Format_InvalidSyntaxExpectation("JSON array", ','),
                        next);
                }
            }

            return new JsonArray(list.ToArray(), head.Line, head.Column);
        }

        private static JsonObject DeserializeObject(JsonToken head, JsonBuffer buffer)
        {
            var dictionary = new Dictionary<string, JsonValue>();

            // Loop through each JSON entry in the input object
            while (true)
            {
                var next = buffer.Read();
                if (next.Type == JsonTokenType.EOF)
                {
                    throw new JsonDeserializerException(
                        JsonDeserializerResource.Format_InvalidSyntaxExpectation("JSON object", '}'),
                        next);
                }

                if (next.Type == JsonTokenType.Colon)
                {
                    throw new JsonDeserializerException(
                        JsonDeserializerResource.Format_InvalidSyntaxNotExpected("JSON object", ':'),
                        next);
                }
                else if (next.Type == JsonTokenType.RightCurlyBracket)
                {
                    break;
                }
                else
                {
                    if (next.Type != JsonTokenType.String)
                    {
                        throw new JsonDeserializerException(
                            JsonDeserializerResource.Format_InvalidSyntaxExpectation("JSON object member name", "JSON string"),
                            next);
                    }

                    var memberName = next.Value;
                    if (dictionary.ContainsKey(memberName))
                    {
                        throw new JsonDeserializerException(
                            JsonDeserializerResource.Format_DuplicateObjectMemberName(memberName),
                            next);
                    }

                    next = buffer.Read();
                    if (next.Type != JsonTokenType.Colon)
                    {
                        throw new JsonDeserializerException(
                            JsonDeserializerResource.Format_InvalidSyntaxExpectation("JSON object", ':'),
                            next);
                    }

                    dictionary[memberName] = DeserializeInternal(buffer.Read(), buffer);

                    next = buffer.Read();
                    if (next.Type == JsonTokenType.RightCurlyBracket)
                    {
                        break;
                    }
                    else if (next.Type != JsonTokenType.Comma)
                    {
                        throw new JsonDeserializerException(
                            JsonDeserializerResource.Format_InvalidSyntaxExpectation("JSON object", ',', '}'),
                            next);
                    }
                }
            }

            return new JsonObject(dictionary, head.Line, head.Column);
        }
    }
}
