// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.ApplicationHost.Impl.Syntax
{
    internal class CommandGrammar : Grammar
    {
        public CommandGrammar(Func<string, string> variable)
        {
            var environmentVariablePiece = Ch('%').And(Rep(Ch().Not(Ch('%')))).And(Ch('%')).Left().Down().Str()
                .Build(key => variable(key) ?? "%" + key + "%");

            var escapeSequencePiece = 
                Ch('%').And(Ch('%')).Build(_=>"%")
                    .Or(Ch('^').And(Ch('^')).Build(_ => "^"))
                    .Or(Ch('\\').And(Ch('\\')).Build(_ => "\\"))
                    .Or(Ch('\\').And(Ch('\"')).Build(_ => "\""))
                ;

            var specialPiece = environmentVariablePiece.Or(escapeSequencePiece);

            var unquotedPiece = Rep1(Ch().Not(specialPiece).Not(Ch(' '))).Str();

            var quotedPiece = Rep1(Ch().Not(specialPiece).Not(Ch('\"'))).Str();

            var unquotedTerm = Rep1(unquotedPiece.Or(specialPiece)).Str();

            var quotedTerm = Ch('\"').And(Rep(quotedPiece.Or(specialPiece)).Str()).And(Ch('\"'))
                .Left().Down()
                .Build(str => "\"" + str + "\"");

            var whitespace = Rep(Ch(' '));

            var term = whitespace.And(quotedTerm.Or(unquotedTerm)).And(whitespace).Left().Down();

            Parse = Rep(term);
        }

        public readonly Parser<IList<string>> Parse;

        public static string[] Process(string text, Func<string, string> variables)
        {
            var grammer = new CommandGrammar(variables);
            var cursor = new Cursor(text, 0, text.Length);

            var result = grammer.Parse(cursor);
            if (!result.Remainder.IsEnd)
            {
                throw new Exception("TODO: malformed command text");
            }
            return result.Value.ToArray();
        }
    }
}