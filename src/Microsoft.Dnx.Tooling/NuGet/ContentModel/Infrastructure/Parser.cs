using System;
using System.Collections.Generic;

namespace NuGet.ContentModel.Infrastructure
{
    public class PatternExpression
    {
        private List<Segment> _segments = new List<Segment>();

        public PatternExpression(string pattern)
        {
            Initialize(pattern);
        }

        private void Initialize(string pattern)
        {
            for (var scanIndex = 0; scanIndex < pattern.Length;)
            {
                var beginToken = (pattern + '{').IndexOf('{', scanIndex);
                var endToken = (pattern + '}').IndexOf('}', beginToken);
                if (scanIndex != beginToken)
                {
                    var literal = pattern.Substring(scanIndex, beginToken - scanIndex);
                    _segments.Add(new LiteralSegment(literal));
                }
                if (beginToken != endToken)
                {
                    var delimiter = (pattern + '\0')[endToken + 1];
                    var matchOnly = pattern[endToken - 1] == '?';

                    var beginName = beginToken + 1;
                    var endName = endToken - (matchOnly ? 1 : 0);

                    var tokenName = pattern.Substring(beginName, endName - beginName);
                    _segments.Add(new TokenSegment(tokenName, delimiter, matchOnly));
                }
                scanIndex = endToken + 1;
            }
        }

        public ContentItem Match(string path, IDictionary<string, ContentPropertyDefinition> propertyDefinitions)
        {
            var item = new ContentItem
            {
                Path = path
            };
            var startIndex = 0;
            foreach (var segment in _segments)
            {
                int endIndex;
                if (segment.TryMatch(item, propertyDefinitions, startIndex, out endIndex))
                {
                    startIndex = endIndex;
                    continue;
                }
                return null;
            }
            return startIndex == path.Length ? item : null;
        }

        private abstract class Segment
        {
            internal abstract bool TryMatch(ContentItem item, IDictionary<string, ContentPropertyDefinition> propertyDefinitions, int startIndex, out int endIndex);
        }

        private class LiteralSegment : Segment
        {
            private readonly string _literal;

            public LiteralSegment(string literal)
            {
                _literal = literal;
            }

            internal override bool TryMatch(
                ContentItem item,
                IDictionary<string, ContentPropertyDefinition> propertyDefinitions,
                int startIndex,
                out int endIndex)
            {
                if (item.Path.Length >= startIndex + _literal.Length)
                {
                    var substring = item.Path.Substring(startIndex, _literal.Length);
                    if (string.Equals(_literal, substring, StringComparison.OrdinalIgnoreCase))
                    {
                        endIndex = startIndex + _literal.Length;
                        return true;
                    }
                }
                endIndex = startIndex;
                return false;
            }
        }

        private class TokenSegment : Segment
        {
            private readonly string _token;
            private readonly char _delimiter;
            private readonly bool _matchOnly;

            public TokenSegment(string token, char delimiter, bool matchOnly)
            {
                _token = token;
                _delimiter = delimiter;
                _matchOnly = matchOnly;
            }

            internal override bool TryMatch(ContentItem item, IDictionary<string, ContentPropertyDefinition> propertyDefinitions, int startIndex, out int endIndex)
            {
                ContentPropertyDefinition propertyDefinition;
                if (!propertyDefinitions.TryGetValue(_token, out propertyDefinition))
                {
                    throw new Exception(string.Format("Unable to find property definition for {{{0}}}", _token));
                }
                for (var scanIndex = startIndex; scanIndex != item.Path.Length;)
                {
                    var delimiterIndex = (item.Path + _delimiter).IndexOf(_delimiter, scanIndex + 1);
                    if (delimiterIndex == item.Path.Length && _delimiter != '\0')
                    {
                        break;
                    }
                    var substring = item.Path.Substring(startIndex, delimiterIndex - startIndex);
                    object value;
                    if (propertyDefinition.TryLookup(substring, out value))
                    {
                        if (!_matchOnly)
                        {
                            item.Properties.Add(_token, value);
                        }
                        endIndex = delimiterIndex;
                        return true;
                    }
                    scanIndex = delimiterIndex;
                }
                endIndex = startIndex;
                return false;
            }
        }
    }
}