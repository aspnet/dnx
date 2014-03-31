using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Net.ApplicationHost.CommandLine
{
    public static class CommandGrammerUtilities
    {
        public static string[] Process(string text, Func<string, string> variables)
        {
            var grammer = new CommandGrammer(variables);
            var cursor = new CommandStringCursor(text, 0, text.Length);

            var result = grammer.Parse(cursor);
            if (!result.Remainder.IsEnd)
            {
                throw new Exception("TODO: malformed command text");
            }
            return result.Value.ToArray();
        }
    }

    struct CommandStringCursor
    {
        private readonly string _text;
        private readonly int _start;
        private readonly int _end;

        public CommandStringCursor(string text, int start, int end)
        {
            _text = text;
            _start = start;
            _end = end;
        }

        public bool IsEnd
        {
            get { return _start == _end; }
        }

        public char Peek(int index)
        {
            return (index + _start) >= _end ? (char)0 : _text[index + _start];
        }

        public Result<TValue> Advance<TValue>(TValue result, int length)
        {
            return new Result<TValue>(result, Advance(length));
        }

        public CommandStringCursor Advance(int length)
        {
            return new CommandStringCursor(_text, _start + length, _end);
        }
    }

    delegate Result<TValue> CommandParser<TValue>(CommandStringCursor cursor);

    class CommandGrammer
    {
        public CommandGrammer(Func<string, string> variable)
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

            var quotedTerm = Ch('\"').And(Rep(quotedPiece.Or(specialPiece)).Str()).And(Ch('\"')).Left().Down();

            var whitespace = Rep(Ch(' '));

            var term = whitespace.And(quotedTerm.Or(unquotedTerm)).And(whitespace).Left().Down();

            Parse = Rep(term);
        }

        public readonly CommandParser<IList<string>> Parse;

        private static CommandParser<IList<TValue>> Rep1<TValue>(CommandParser<TValue> parser)
        {
            CommandParser<IList<TValue>> rep = Rep(parser);
            return pos =>
            {
                var result = rep(pos);
                return result.IsEmpty || !result.Value.Any() ? Result<IList<TValue>>.Empty : result;
            };
        }

        private static CommandParser<IList<TValue>> Rep<TValue>(CommandParser<TValue> parser)
        {
            return pos =>
            {
                var data = new List<TValue>();
                for (; ; )
                {
                    var result = parser(pos);
                    if (result.IsEmpty) break;
                    data.Add(result.Value);
                    pos = result.Remainder;
                }
                return new Result<IList<TValue>>(data, pos);
            };
        }

        private static CommandParser<char> Ch()
        {
            return pos => pos.IsEnd ? Result<char>.Empty : pos.Advance(pos.Peek(0), 1);
        }

        private static CommandParser<bool> IsEnd()
        {
            return pos => pos.IsEnd ? pos.Advance(true, 0) : Result<bool>.Empty;
        }

        private static CommandParser<char> Ch(char ch)
        {
            return pos => pos.Peek(0) != ch ? Result<char>.Empty : pos.Advance(ch, 1);
        }
    }

    static class CommandParserExtensions
    {
        public static CommandParser<Chain<T1, T2>> And<T1, T2>(this CommandParser<T1> parser1,
            CommandParser<T2> parser2)
        {
            return pos =>
            {
                var result1 = parser1(pos);
                if (result1.IsEmpty) return Result<Chain<T1, T2>>.Empty;
                var result2 = parser2(result1.Remainder);
                if (result2.IsEmpty) return Result<Chain<T1, T2>>.Empty;
                return new Result<Chain<T1, T2>>(new Chain<T1, T2>(result1.Value, result2.Value), result2.Remainder);
            };
        }

        public static CommandParser<T1> Or<T1>(this CommandParser<T1> parser1, CommandParser<T1> parser2)
        {
            return pos =>
            {
                var result1 = parser1(pos);
                if (!result1.IsEmpty) return result1;
                var result2 = parser2(pos);
                if (!result2.IsEmpty) return result2;
                return Result<T1>.Empty;
            };
        }

        public static CommandParser<T1> Not<T1, T2>(this CommandParser<T1> parser1, CommandParser<T2> parser2)
        {
            return pos =>
            {
                var result2 = parser2(pos);
                if (!result2.IsEmpty) return Result<T1>.Empty;
                return parser1(pos);
            };
        }

        public static CommandParser<T1> Left<T1, T2>(this CommandParser<Chain<T1, T2>> parser)
        {
            return pos =>
            {
                var result = parser(pos);
                if (result.IsEmpty) return Result<T1>.Empty;
                return new Result<T1>(result.Value.Left, result.Remainder);
            };
        }

        public static CommandParser<T2> Down<T1, T2>(this CommandParser<Chain<T1, T2>> parser)
        {
            return pos =>
            {
                var result = parser(pos);
                if (result.IsEmpty) return Result<T2>.Empty;
                return new Result<T2>(result.Value.Down, result.Remainder);
            };
        }

        public static CommandParser<T2> Build<T1, T2>(this CommandParser<T1> parser, Func<T1, T2> builder)
        {
            return pos =>
            {
                var result = parser(pos);
                if (result.IsEmpty) return Result<T2>.Empty;
                return new Result<T2>(builder(result.Value), result.Remainder);
            };
        }

        public static CommandParser<string> Str(this CommandParser<IList<char>> parser)
        {
            return parser.Build(x => new string(x.ToArray()));
        }

        public static CommandParser<string> Str(this CommandParser<IList<string>> parser)
        {
            return parser.Build(x => String.Concat(x.ToArray()));
        }
    }

    struct Result<TValue>
    {
        public Result(TValue value, CommandStringCursor remainder)
            : this()
        {
            Value = value;
            Remainder = remainder;
        }

        public readonly TValue Value;
        public readonly CommandStringCursor Remainder;

        public bool IsEmpty
        {
            get { return Equals(this, default(Result<TValue>)); }
        }

        public static Result<TValue> Empty
        {
            get { return default(Result<TValue>); }
        }
    }

    struct Chain<TLeft, TDown>
    {
        public Chain(TLeft left, TDown down)
            : this()
        {
            Left = left;
            Down = down;
        }

        public readonly TLeft Left;
        public readonly TDown Down;
    }
}
