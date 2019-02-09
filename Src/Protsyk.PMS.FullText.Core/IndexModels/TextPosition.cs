using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Protsyk.PMS.FullText.Core
{
    public struct TextPosition : IEquatable<TextPosition>, IComparable<TextPosition>
    {
        #region Fields
        public static readonly TextPosition Empty = P(0, 0);
        private static readonly Regex regParse = new Regex("^\\[(?<offset>\\d+),(?<length>\\d+)\\]$",
                                                           RegexOptions.Compiled | RegexOptions.Singleline);

        public readonly int Offset;
        public readonly int Length;
        #endregion

        #region Static Methods
        /// <summary>
        /// Construct occurrence
        /// </summary>
        public static TextPosition P(int offset, int length)
        {
            return new TextPosition(offset, length);
        }

        public static TextPosition Parse(string text)
        {
            var match = regParse.Match(text);
            if (!match.Success)
            {
                throw new InvalidOperationException($"Occurrence text has invalid format: {text}");
            }

            return P(
                int.Parse(match.Groups["offset"].Value),
                int.Parse(match.Groups["length"].Value));
        }
        #endregion

        #region Methods
        public TextPosition(int offset, int length)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            this.Offset = offset;
            this.Length = length;
        }

        public override string ToString()
        {
            return $"[{Offset},{Length}]";
        }
        #endregion

        #region IEquatable<TextPosition>
        public bool Equals(TextPosition other)
        {
            return Offset == other.Offset &&
                   Length == other.Length;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            return obj is TextPosition && Equals((TextPosition) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Offset, Length);
        }
        #endregion

        #region IComparable
        public int CompareTo(TextPosition other)
        {
            var offsetComparison = Offset.CompareTo(other.Offset);
            if (offsetComparison != 0)
            {
                return offsetComparison;
            }

            return Length.CompareTo(other.Length);
        }
        #endregion

        #region Operators
        public static bool operator <(TextPosition l, TextPosition r)
        {
            return l.CompareTo(r) < 0;
        }

        public static bool operator >(TextPosition l, TextPosition r)
        {
            return l.CompareTo(r) > 0;
        }

        public static bool operator ==(TextPosition l, TextPosition r)
        {
            return l.Equals(r);
        }

        public static bool operator !=(TextPosition l, TextPosition r)
        {
            return !l.Equals(r);
        }
        #endregion
    }
}
