// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.Runtime.Json
{
    internal class JsonDeserializerResource
    {
        internal static string JSON_BadEscape
        {
            get { return "Unrecognized escape sequence."; }
        }

        internal static string JSON_DepthLimitExceeded
        {
            get { return "RecursionLimit exceeded."; }
        }

        internal static string JSON_ExpectedOpenBrace
        {
            get { return "Invalid object passed in, '{' expected."; }
        }

        internal static string JSON_IllegalPrimitive
        {
            get { return "Invalid JSON primitive: {0}."; }
        }

        internal static string JSON_InvalidArrayEnd
        {
            get { return "Invalid array passed in, ']' expected."; }
        }

        internal static string JSON_InvalidArrayExpectComma
        {
            get { return "Invalid array passed in, ',' expected."; }
        }

        internal static string JSON_InvalidArrayExtraComma
        {
            get { return "Invalid array passed in, extra trailing ','."; }
        }

        internal static string JSON_InvalidArrayStart
        {
            get { return "Invalid array passed in, '[' expected."; }
        }

        internal static string JSON_InvalidMaxJsonLength
        {
            get { return "Value must be a positive integer."; }
        }

        internal static string JSON_InvalidMemberName
        {
            get { return "Invalid object passed in, member name expected."; }
        }

        internal static string JSON_InvalidObject
        {
            get { return "Invalid object passed in, ':' or '}' expected."; }
        }

        internal static string JSON_InvalidRecursionLimit
        {
            get { return "RecursionLimit must be a positive integer."; }
        }

        internal static string JSON_MaxJsonLengthExceeded
        {
            get { return "Error during serialization or deserialization using the JSON JavaScriptSerializer. The length of the string exceeds the value set on the maxJsonLength property."; }
        }

        internal static string JSON_StringNotQuoted
        {
            get { return "Invalid string passed in, '\\\"' expected."; }
        }

        internal static string JSON_UnterminatedString
        {
            get { return "Unterminated string passed in."; }
        }

        internal static string JSON_MaxJsonDeserializerMembers
        {
            get { return "The maximum number of items has already been deserialized into a single dictionary by the JavaScriptSerializer.The value is '{0}'."; }
        }
    }
}
