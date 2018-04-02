using System.Collections.Generic;
using System.Text;

namespace Protsyk.PMS.FullText.Core
{
    public abstract class AstQuery
    {
        public readonly string Name;

        protected AstQuery(string name)
        {
            Name = name;
        }

        protected internal abstract StringBuilder ToString(StringBuilder builder);

        public override string ToString()
        {
            return ToString(new StringBuilder()).ToString();
        }
    }

    public class FunctionAstQuery : AstQuery
    {
        public readonly List<AstQuery> Args = new List<AstQuery>();

        public FunctionAstQuery(string name)
            :base(name)
        {
        }

        protected internal override StringBuilder ToString(StringBuilder builder)
        {
            builder.Append(Name);
            if (Args.Count > 0)
            {
                builder.Append('(');
                var first = true;
                foreach (var child in Args)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }
                    builder = child.ToString(builder);
                    first = false;
                }
                builder.Append(')');
            }
            return builder;
        }
    }

    public abstract class TermAstQuery : AstQuery
    {
        public readonly string Value;

        public readonly string EscapedValue;

        public TermAstQuery(string name, string value, string escapedValue)
            : base(name)
        {
            this.Value = value;
            this.EscapedValue = escapedValue;
        }

        protected internal override StringBuilder ToString(StringBuilder builder)
        {
            builder.Append(Name);
            builder.Append('(');
            builder.Append(EscapedValue);
            builder.Append(')');
            return builder;
        }
    }

    public class WordAstQuery : TermAstQuery
    {
        public WordAstQuery(string name, string value, string escapedValue)
            : base(name, value, escapedValue)
        {
        }
    }

    public class WildcardAstQuery : TermAstQuery
    {
        public WildcardAstQuery(string name, string value, string escapedValue)
            : base(name, value, escapedValue)
        {
        }
    }

    public class EditAstQuery : TermAstQuery
    {
        public readonly int Distance;

        public EditAstQuery(string name, string value, string escapedValue, int distance)
            : base(name, value, escapedValue)
        {
            this.Distance = distance;
        }

        protected internal override StringBuilder ToString(StringBuilder builder)
        {
            builder.Append(Name);
            builder.Append('(');
            builder.Append(EscapedValue);
            builder.Append(',');
            builder.Append(Distance);
            builder.Append(')');
            return builder;
        }
    }
}
