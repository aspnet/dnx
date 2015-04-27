// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.Runtime.Json
{
    internal class JsonDeserializerResource
    {
        internal static string Format_IllegalCharacter(int value)
        {
            return string.Format("Illegal character {0} {0:X4}.", (char)value, value);
        }

        internal static string Format_IllegalTrailingCharacterAfterLiteral(int value, string literal)
        {
            return string.Format("Illegal character {0} ({1:X4}) after the literal name {2}.",
                (char)value,
                value,
                literal);
        }

        internal static string Format_UnrecognizedLiteral(string literal)
        {
            return string.Format("Invalid JSON literal. {0} is not legal JSON literal.", literal);
        }

        internal static string Format_UnexpectedToken(string tokenValue, JsonTokenType type)
        {
            return string.Format("Unexpected token, type: {0} value: {1}.", type.ToString(), tokenValue);
        }

        internal static string Format_DuplicateObjectMemberName(string memberName)
        {
            return Format_InvalidSyntax("JSON object", string.Format("Duplicate member name {0}.", memberName));
        }

        internal static string Format_InvalidFloatNumberFormat(string raw)
        {
            return string.Format("Invalid float number format: {0}", raw);
        }

        internal static string Format_FloatNumberOverflow(string raw)
        {
            return string.Format("Float number overflow: {0}", raw);
        }

        internal static string Format_InvalidSyntax(string syntaxName, string issue)
        {
            return string.Format("Invalid {0} syntax. {1}.");
        }

        internal static string Format_InvalidSyntaxNotExpected(string syntaxName, char unexpected)
        {
            return string.Format("Invalid {0} syntax. Unexpected '{1}'.", syntaxName, unexpected);
        }

        internal static string Format_InvalidSyntaxNotExpected(string syntaxName, string unexpected)
        {
            return string.Format("Invalid {0} syntax. Unexpected {1}.", syntaxName, unexpected);
        }

        internal static string Format_InvalidSyntaxExpectation(string syntaxName, char expectation)
        {
            return string.Format("Invalid {0} syntax. Expected '{1}'.", syntaxName, expectation);
        }

        internal static string Format_InvalidSyntaxExpectation(string syntaxName, string expectation)
        {
            return string.Format("Invalid {0} syntax. Expected {1}.", syntaxName, expectation);
        }

        internal static string Format_InvalidSyntaxExpectation(string syntaxName, char expectation1, char expectation2)
        {
            return string.Format("Invalid {0} syntax. Expected '{1}' or '{2}'.", syntaxName, expectation1, expectation2);
        }

        internal static string Format_InvalidUnicode(string unicode)
        {
            return string.Format("Invalid Unicode [{0}]", unicode);
        }

        internal static string Format_UnfinishedJSON(string nextTokenValue)
        {
            return string.Format("Invalid JSON end. Unprocessed token {0}.", nextTokenValue);
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
