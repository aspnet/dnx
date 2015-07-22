// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Runtime.Json
{
    internal class JsonDeserializerResource
    {
        internal static string Format_IllegalCharacter(int value)
        {
            return $"Illegal character '{(char)value}' (Unicode hexadecimal {value:X4}).";
        }

        internal static string Format_IllegalTrailingCharacterAfterLiteral(int value, string literal)
        {
            return $"Illegal character '{(char)value}' (Unicode hexadecimal {value:X4}) after the literal name '{literal}'.";
        }

        internal static string Format_UnrecognizedLiteral(string literal)
        {
            return $"Invalid JSON literal. Expected literal '{literal}'.";
        }

        internal static string Format_DuplicateObjectMemberName(string memberName)
        {
            return Format_InvalidSyntax("JSON object", $"Duplicate member name '{memberName}'");
        }

        internal static string Format_InvalidFloatNumberFormat(string raw)
        {
            return $"Invalid float number format: {raw}";
        }

        internal static string Format_FloatNumberOverflow(string raw)
        {
            return $"Float number overflow: {raw}";
        }

        internal static string Format_InvalidSyntax(string syntaxName, string issue)
        {
            return $"Invalid {syntaxName} syntax. {issue}.";
        }

        internal static string Format_InvalidSyntaxNotExpected(string syntaxName, char unexpected)
        {
            return $"Invalid {syntaxName} syntax. Unexpected '{unexpected}'.";
        }

        internal static string Format_InvalidSyntaxNotExpected(string syntaxName, string unexpected)
        {
            return $"Invalid {syntaxName} syntax. Unexpected {unexpected}.";
        }

        internal static string Format_InvalidSyntaxExpectation(string syntaxName, char expectation)
        {
            return $"Invalid {syntaxName} syntax. Expected '{expectation}'.";
        }

        internal static string Format_InvalidSyntaxExpectation(string syntaxName, string expectation)
        {
            return $"Invalid {syntaxName} syntax. Expected {expectation}.";
        }

        internal static string Format_InvalidSyntaxExpectation(string syntaxName, char expectation1, char expectation2)
        {
            return $"Invalid {syntaxName} syntax. Expected '{expectation1}' or '{expectation2}'.";
        }
        
        internal static string Format_InvalidTokenExpectation(string tokenValue, string expectation)
        {
            return $"Unexpected token '{tokenValue}'. Expected {expectation}.";
        }

        internal static string Format_InvalidUnicode(string unicode)
        {
            return $"Invalid Unicode [{unicode}]";
        }

        internal static string Format_UnfinishedJSON(string nextTokenValue)
        {
            return $"Invalid JSON end. Unprocessed token {nextTokenValue}.";
        }

        internal static string JSON_OpenString
        {
            get { return Format_InvalidSyntaxExpectation("JSON string", '\"'); }
        }

        internal static string JSON_InvalidEnd
        {
            get { return "Invalid JSON. Unexpected end of file."; }
        }
    }
}
