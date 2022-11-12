using System;
using System.Collections.Generic;
using System.Text;

namespace Protsyk.PMS.FullText.Core
{
    public class QueryParser
    {
        private static readonly Dictionary<string, Func<string, int, string, ParseResult>> argumentsParsers = new()
        {
            { "OR", ParseArguments },
            { "AND", ParseArguments },
            { "SEQ", ParseArguments },

            { "WORD", ParseWord },
            { "WILD", ParseWildcard },
            { "EDIT", ParseEdit }
        };

        private static readonly HashSet<char> specialChars = new HashSet<char>()
        {
            ',',
            '(',
            ')',

            '\\',

            '~',

            '*',
            '?'
        };

        private static readonly HashSet<char> whitespaceCharacters = new HashSet<char>()
        {
            ' ',
            '\t'
        };

        public AstQuery Parse(string s)
        {
            var result = Parse(s, 0);
            if (result.position != s.Length)
            {
                throw new QueryParserException("Unexpected text", result.position);
            }
            return result.query;
        }

        private static ParseResult Parse(string s, int startIndex)
        {
            int pos = startIndex;

            pos = SkipWhitespace(s, pos);
            EnsureNotAtEnd(s, pos);

            // Parse name
            int nameStart = pos;
            while (pos < s.Length && char.IsUpper(s[pos]))
            {
                ++pos;
            }
            int nameEnd = pos;

            if (nameStart == nameEnd)
                throw new QueryParserException("Empty operation name");

            pos = SkipWhitespace(s, pos);
            EnsureNotAtEnd(s, pos);

            if (s[pos] != '(')
                throw new QueryParserException("Expected character (", pos);
            ++pos;

            var name = s.Substring(nameStart, nameEnd - nameStart);

            if (!argumentsParsers.TryGetValue(name, out var argumentParser))
            {
                throw new QueryParserException($"no parser for arguments of {name}");
            }

            var subResult = argumentParser(s, pos, name);

            pos = subResult.position;
            pos = SkipWhitespace(s, pos);

            EnsureNotAtEnd(s, pos);

            if (s[pos] != ')')
                throw new QueryParserException("Expected character )", pos);
            ++pos;

            return new ParseResult(subResult.query, pos);
        }

        private static ParseResult ParseArguments(string s, int pos, string name)
        {
            var query = new FunctionAstQuery(name);

            while (pos < s.Length && !specialChars.Contains(s[pos]))
            {
                var result = Parse(s, pos);

                query.Args.Add(result.query);
                pos = result.position;

                pos = SkipWhitespace(s, pos);

                if (pos < s.Length && s[pos] != ',')
                {
                    break;
                }
                else
                {
                    ++pos;
                }
            }

            return new ParseResult(query, pos);
        }

        private static ParseResult ParseWord(string s, int pos, string name)
        {
            pos = SkipWhitespace(s, pos);
            var word = new StringBuilder();
            var escaped = new StringBuilder();

            while (pos < s.Length)
            {
                if (s[pos] == '\\')
                {
                    escaped.Append('\\');
                    pos = ParseEscapedCharacter(s, pos);
                }
                else if (IsStopCharacter(s[pos]))
                {
                    var value = word.ToString();
                    return new ParseResult(new WordAstQuery(name, value, escaped.ToString()), pos);
                }

                escaped.Append(s[pos]);
                word.Append(s[pos]);
                ++pos;
            }

            throw new QueryParserException("Expected value", pos);
        }

        private static ParseResult ParseWildcard(string s, int pos, string name)
        {
            pos = SkipWhitespace(s, pos);
            var word = new StringBuilder();
            var escaped = new StringBuilder();

            while (pos < s.Length)
            {
                if (s[pos] == '\\')
                {
                    escaped.Append('\\');
                    pos = ParseEscapedCharacter(s, pos);
                }
                else if (s[pos] is '*' or '?')
                {
                    // Accepted wildcard characters
                }
                else if (IsStopCharacter(s[pos]))
                {
                    return new ParseResult(new WildcardAstQuery(name, word.ToString(), escaped.ToString()), pos);
                }

                escaped.Append(s[pos]);
                word.Append(s[pos]);
                ++pos;
            }

            throw new QueryParserException("Expected value", pos);
        }

        private static ParseResult ParseEdit(string s, int pos, string name)
        {
            pos = SkipWhitespace(s, pos);

            var word = ParseWord(s, pos, "WORD");
            pos = word.position;

            pos = SkipWhitespace(s, pos);
            if (pos < s.Length && s[pos] != ',')
            {
                throw new QueryParserException("Expected character ,", pos);
            }
            else
            {
                ++pos;
            }

            var distance = ParseWord(s, pos, "WORD");
            pos = distance.position;

            pos = SkipWhitespace(s, pos);

            var wordQuery = (WordAstQuery)word.query;
            var distQuery = (WordAstQuery)distance.query;

            return new ParseResult(new EditAstQuery(name, wordQuery.Value, wordQuery.EscapedValue, int.Parse(distQuery.Value)), pos);
        }

        private static int ParseEscapedCharacter(string s, int pos)
        {
            if (pos + 1 < s.Length)
            {
                if (!specialChars.Contains(s[pos + 1]))
                {
                    throw new QueryParserException("invalid escape character", pos + 1);
                }

                ++pos;
            }
            else
            {
                throw new QueryParserException("expected escape character", pos);
            }

            return pos;
        }

        private static void EnsureNotAtEnd(string s, int pos)
        {
            if (pos == s.Length)
                throw new QueryParserException("Unexpected end of query");
        }

        private static bool IsStopCharacter(char c)
        {
            return whitespaceCharacters.Contains(c) || specialChars.Contains(c);
        }

        private static int SkipWhitespace(string s, int pos)
        {
            while (pos < s.Length && whitespaceCharacters.Contains(s[pos]))
            {
                pos++;
            }

            return pos;
        }

        private readonly struct ParseResult
        {
            public readonly AstQuery query;
            public readonly int position;

            public ParseResult(AstQuery query, int position)
            {
                this.query = query;
                this.position = position;
            }
        }
    }
}
