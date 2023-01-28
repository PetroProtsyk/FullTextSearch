using System;

namespace Protsyk.PMS.FullText.Core;

public class PhraseQuery : ISearchQuery
{
    #region Fields
    private readonly ISearchQuery[] queries;
    private readonly IMatch[] matches;
    private bool consumed;
    #endregion

    #region Methods
    public PhraseQuery(params ISearchQuery [] queries)
    {
        this.queries = queries;
        this.matches = new IMatch[queries.Length];
        this.consumed = false;
    }
    #endregion

    #region ISearchQuery
    public IMatch NextMatch()
    {
        if (consumed)
        {
            return null;
        }

        var target = Occurrence.Empty;
        var i = 0;

        matches[i] = null;
        while (i < queries.Length)
        {
            var newTarget = Occurrence.Empty;

            while (matches[i] == null || matches[i].Right < target)
            {
                var m = queries[i].NextMatch();
                matches[i] = m;
                if (m == null)
                {
                    consumed = true;
                    return null;
                }

                newTarget = new Occurrence(
                    Math.Max(target.DocumentId, m.Right.DocumentId),
                    Math.Max(target.FieldId, m.Right.FieldId),
                    0);
            }


            if (target < newTarget)
            {
                target = newTarget;
                i = 0;
            }
            else if (i == 0)
            {
                i++;
            }
            else if (matches[i - 1].Right.TokenId + 1 == matches[i].Left.TokenId)
            {
                i++;
            }
            else
            {
                matches[i] = null;
            }
        }

        return new SequenceMatch(matches);
    }


    public bool AreCompatible(IMatch[] matches)
    {
        var o = matches[0].Left;
        for (int i = 1; i < matches.Length; i++)
        {
            if (o.DocumentId != matches[i].Left.DocumentId ||
                o.FieldId != matches[i].Left.FieldId)
            {
                return false;
            }
        }
        return true;
    }

    public void Dispose()
    {
    }
    #endregion
}
