// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Microsoft.Framework.Runtime.Json
{
    internal class JsonDeserializer
    {
        // maximum number of entries a Json deserialized dictionary is allowed to have
        private const int _maxJsonDeserializerMembers = Int32.MaxValue;
        private const int _maxDeserializeDepth = 100;
        private const int _maxInputLength = 2097152;

        private JsonContent _input;

        public JsonValue Deserialize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            _input = JsonContent.CreateFromStream(stream);

            return Deserialize();
        }

        public JsonValue Deserialize(string input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (input.Length > _maxInputLength)
            {
                throw new ArgumentException(JsonDeserializerResource.JSON_MaxJsonLengthExceeded, nameof(input));
            }

            _input = JsonContent.CreateFromString(input);

            return Deserialize();
        }

        private JsonValue Deserialize()
        {
            JsonValue result = DeserializeInternal(0);

            // There are still unprocessed char. The parsing is not finished. Error happened.
            if (_input.MoveToNextNonEmptyChar() == true)
            {
                throw CreateExceptionFromContent(JsonDeserializerResource.JSON_IllegalPrimitive);
            }

            return result;
        }

        private JsonValue DeserializeInternal(int depth)
        {
            if (++depth > _maxDeserializeDepth)
            {
                throw CreateExceptionFromContent(JsonDeserializerResource.JSON_DepthLimitExceeded);
            }

            if (!_input.MoveToNextNonEmptyChar())
            {
                return null;
            }

            var nextChar = _input.CurrentChar;
            _input.MovePrev();

            if (IsNextElementObject(nextChar))
            {
                return DeserializeDictionary(depth);
            }

            if (IsNextElementArray(nextChar))
            {
                return DeserializeList(depth);
            }

            if (IsNextElementString(nextChar))
            {
                return DeserializeString();
            }

            return DeserializePrimitiveObject();
        }

        private JsonDeserializerException CreateExceptionFromContent(string message)
        {
            message = message ?? "Failed to deserialize. ";

            return new JsonDeserializerException(
                _input.GetStatusInfo(message),
                _input.CurrentLine,
                _input.CurrentColumn);
        }

        private JsonArray DeserializeList(int depth)
        {
            var list = new List<JsonValue>();

            if (!_input.MoveNext())
            {
                throw CreateExceptionFromContent("Parsing reach the end of the content before it can finish");
            }

            if (!_input.ValidCursor)
            {
                throw CreateExceptionFromContent("Invalid cursor");
            }

            if (_input.CurrentChar != '[')
            {
                throw CreateExceptionFromContent(JsonDeserializerResource.JSON_InvalidArrayStart);
            }

            var position = new JsonPosition(_input.CurrentLine, _input.CurrentColumn);

            bool expectMore = false;
            while (_input.MoveToNextNonEmptyChar() && _input.CurrentChar != ']')
            {
                _input.MovePrev();

                JsonValue o = DeserializeInternal(depth);
                list.Add(o);

                expectMore = false;

                // we might be done here.
                _input.MoveToNextNonEmptyChar();
                if (_input.CurrentChar == ']')
                {
                    break;
                }

                expectMore = true;
                if (_input.CurrentChar != ',')
                {
                    throw CreateExceptionFromContent(JsonDeserializerResource.JSON_InvalidArrayExpectComma);
                }
            }

            if (expectMore)
            {
                throw CreateExceptionFromContent(JsonDeserializerResource.JSON_InvalidArrayExtraComma);
            }

            if (_input.CurrentChar != ']')
            {
                throw CreateExceptionFromContent(JsonDeserializerResource.JSON_InvalidArrayEnd);
            }

            return new JsonArray(list.ToArray(), position);
        }

        private JsonObject DeserializeDictionary(int depth)
        {
            IDictionary<string, JsonValue> dictionary = null;

            if (!_input.MoveNext())
            {
                throw CreateExceptionFromContent("Parsing reach the end of the content before it can finish");
            }

            if (!_input.ValidCursor)
            {
                throw CreateExceptionFromContent("Invalid cursor");
            }

            if (_input.CurrentChar != '{')
            {
                throw CreateExceptionFromContent(JsonDeserializerResource.JSON_ExpectedOpenBrace);
            }

            var position = new JsonPosition(_input.CurrentLine, _input.CurrentColumn);

            // Loop through each JSON entry in the input object
            while (_input.MoveToNextNonEmptyChar())
            {
                char c = _input.CurrentChar;

                _input.MovePrev();

                if (c == ':')
                {
                    throw CreateExceptionFromContent(JsonDeserializerResource.JSON_InvalidMemberName);
                }

                string memberName = null;
                if (c != '}')
                {
                    // Find the member name
                    memberName = DeserializeMemberName();
                    _input.MoveToNextNonEmptyChar();
                    if (_input.CurrentChar != ':')
                    {
                        throw CreateExceptionFromContent(JsonDeserializerResource.JSON_InvalidObject);
                    }
                }

                if (dictionary == null)
                {
                    dictionary = new Dictionary<string, JsonValue>();

                    // If the object contains nothing (i.e. {}), we're done
                    if (memberName == null)
                    {
                        // Move the cursor to the '}' character.
                        _input.MoveToNextNonEmptyChar();
                        break;
                    }
                }

                ThrowIfMaxJsonDeserializerMembersExceeded(dictionary.Count);

                // Deserialize the property value.  Here, we don't know its type
                dictionary[memberName] = DeserializeInternal(depth);
                _input.MoveToNextNonEmptyChar();
                if (_input.CurrentChar == '}')
                {
                    break;
                }

                if (_input.CurrentChar != ',')
                {
                    throw CreateExceptionFromContent(JsonDeserializerResource.JSON_InvalidObject);
                }
            }

            if (_input.CurrentChar != '}')
            {
                throw CreateExceptionFromContent(JsonDeserializerResource.JSON_InvalidObject);
            }

            return new JsonObject(dictionary, position);
        }

        // Deserialize a member name.
        // e.g. { MemberName: ... }
        // e.g. { 'MemberName': ... }
        // e.g. { "MemberName": ... }
        private string DeserializeMemberName()
        {
            // It could be double quoted, single quoted, or not quoted at all
            if (!_input.MoveToNextNonEmptyChar())
            {
                return null;
            }

            var c = _input.CurrentChar;
            _input.MovePrev();

            // If it's quoted, treat it as a string
            if (IsNextElementString(c))
            {
                return DeserializeString();
            }

            // Non-quoted token
            return DeserializePrimitiveToken().String;
        }

        private JsonPrimitive DeserializePrimitiveObject()
        {
            JsonToken token = DeserializePrimitiveToken();
            if (token.String.Equals("null"))
            {
                return new JsonNull(token.Position);
            }

            if (token.String.Equals("true"))
            {
                return new JsonBoolean(true, token.Position);
            }

            if (token.String.Equals("false"))
            {
                return new JsonBoolean(false, token.Position);
            }

            // Is it a floating point value
            bool hasDecimalPoint = token.String.IndexOf('.') >= 0;

            // DevDiv 56892: don't try to parse to Int32/64/Decimal if it has an exponent sign
            bool hasExponent = token.String.LastIndexOf("e", StringComparison.OrdinalIgnoreCase) >= 0;
            // [Last]IndexOf(char, StringComparison) overload doesn't exist, so search for "e" as a string not a char
            // Use 'Last'IndexOf since if there is an exponent it would be more quickly found starting from the end of the string
            // since 'e' is always toward the end of the number. e.g. 1.238907598768972987E82

            if (!hasExponent)
            {
                // when no exponent, could be Int32, Int64, Decimal, and may fall back to Double
                // otherwise it must be Double

                if (!hasDecimalPoint)
                {
                    // No decimal or exponent. All Int32 and Int64s fall into this category, so try them first
                    // First try int
                    int n;
                    if (int.TryParse(token.String, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                    {
                        // NumberStyles.Integer: AllowLeadingWhite, AllowTrailingWhite, AllowLeadingSign
                        return new JsonInteger(n, token.Position);
                    }

                    // Then try a long
                    long l;
                    if (long.TryParse(token.String, NumberStyles.Integer, CultureInfo.InvariantCulture, out l))
                    {
                        // NumberStyles.Integer: AllowLeadingWhite, AllowTrailingWhite, AllowLeadingSign
                        return new JsonLong(l, token.Position);
                    }
                }

                // No exponent, may or may not have a decimal (if it doesn't it couldn't be parsed into Int32/64)
                decimal dec;
                if (decimal.TryParse(token.String, NumberStyles.Number, CultureInfo.InvariantCulture, out dec))
                {
                    // NumberStyles.Number: AllowLeadingWhite, AllowTrailingWhite, AllowLeadingSign,
                    //                      AllowTrailingSign, AllowDecimalPoint, AllowThousands
                    return new JsonDecimal(dec, token.Position);
                }
            }

            // either we have an exponent or the number couldn't be parsed into any previous type. 
            double d;
            if (double.TryParse(token.String, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            {
                // NumberStyles.Float: AllowLeadingWhite, AllowTrailingWhite, AllowLeadingSign, AllowDecimalPoint, AllowExponent
                return new JsonDouble(d, token.Position);
            }

            // must be an illegal primitive
            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, JsonDeserializerResource.JSON_IllegalPrimitive, token.String));
        }

        private JsonToken DeserializePrimitiveToken()
        {
            var sb = new StringBuilder();

            bool firstChar = true;
            int line = 0;
            int column = 0;

            while (_input.MoveNext())
            {
                var c = _input.CurrentChar;
                if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == '+')
                {
                    sb.Append(c);

                    if (firstChar)
                    {
                        line = _input.CurrentLine;
                        column = _input.CurrentColumn;
                        firstChar = false;
                    }
                }
                else
                {
                    _input.MovePrev();
                    break;
                }
            }

            return new JsonToken(sb.ToString(), line, column);
        }

        private JsonString DeserializeString()
        {
            var sb = new StringBuilder();
            var escapedChar = false;

            _input.MoveNext();

            int line = _input.CurrentLine;
            int column = _input.CurrentColumn;

            // First determine which quote is used by the string.
            var quoteChar = CheckQuoteChar(_input.CurrentChar);
            while (_input.MoveNext())
            {
                if (_input.CurrentChar == '\\')
                {
                    if (escapedChar)
                    {
                        sb.Append('\\');
                        escapedChar = false;
                    }
                    else
                    {
                        escapedChar = true;
                    }

                    continue;
                }

                if (escapedChar)
                {
                    AppendCharToBuilder(_input.CurrentChar, sb);
                    escapedChar = false;
                }
                else
                {
                    if (_input.CurrentChar == quoteChar)
                    {
                        var stringValue = Utf16StringValidator.ValidateString(sb.ToString());
                        var token = new JsonToken(stringValue, line, column);
                        return new JsonString(stringValue, token);
                    }

                    sb.Append(_input.CurrentChar);
                }
            }

            throw CreateExceptionFromContent(JsonDeserializerResource.JSON_UnterminatedString);
        }

        private void AppendCharToBuilder(char? c, StringBuilder sb)
        {
            if (c == '"' || c == '\'' || c == '/')
            {
                sb.Append(c.Value);
            }
            else if (c == 'b')
            {
                sb.Append('\b');
            }
            else if (c == 'f')
            {
                sb.Append('\f');
            }
            else if (c == 'n')
            {
                sb.Append('\n');
            }
            else if (c == 'r')
            {
                sb.Append('\r');
            }
            else if (c == 't')
            {
                sb.Append('\t');
            }
            else if (c == 'u')
            {
                var c4 = new char[4];
                for (int i = 0; i < 4; ++i)
                {
                    _input.MoveNext();
                    c4[i] = _input.CurrentChar;
                }

                sb.Append((char)int.Parse(new string(c4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }
            else
            {
                throw CreateExceptionFromContent(JsonDeserializerResource.JSON_BadEscape);
            }
        }

        private char CheckQuoteChar(char c)
        {
            var quoteChar = '"';
            if (c == '\'')
            {
                quoteChar = c;
            }
            else if (c != '"')
            {
                // Fail if the string is not quoted.
                throw CreateExceptionFromContent(JsonDeserializerResource.JSON_StringNotQuoted);
            }

            return quoteChar;
        }

        // MSRC 12038: limit the maximum number of entries that can be added to a Json deserialized dictionary,
        // as a large number of entries potentially can result in too many hash collisions that may cause DoS
        private void ThrowIfMaxJsonDeserializerMembersExceeded(int count)
        {
            if (count >= _maxJsonDeserializerMembers)
            {
                throw new InvalidOperationException(string.Format(JsonDeserializerResource.JSON_MaxJsonDeserializerMembers, _maxJsonDeserializerMembers));
            }
        }

        private static bool IsNextElementArray(char? c)
        {
            return c == '[';
        }

        private static bool IsNextElementObject(char? c)
        {
            return c == '{';
        }

        private static bool IsNextElementString(char? c)
        {
            return c == '"' || c == '\'';
        }
    }
}
