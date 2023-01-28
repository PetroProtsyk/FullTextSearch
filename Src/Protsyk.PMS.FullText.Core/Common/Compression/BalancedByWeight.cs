using System.Runtime.InteropServices;

namespace Protsyk.PMS.FullText.Core.Common.Compression;

// Variation of Shannon Fano coding
// https://en.wikipedia.org/wiki/Shannon%E2%80%93Fano_coding
public class BalancedByWeightBuilder : VarLenCharEncodingBuilder
{
    private readonly int optimizationType;

    public BalancedByWeightBuilder()
        : this(1)
    {
    }

    public BalancedByWeightBuilder(int type)
    {
        optimizationType = type;
    }

    protected override VarLenCharEncoding DoBuild()
    {
        return new BalancedByWeightEncoding(DivEqually(CollectionsMarshal.AsSpan(symbols)));
    }

    private double Score(BaseNode n, int depth)
    {
        if (n is Node d)
        {
            return Score(d.left, depth + 1) + Score(d.right, depth + 1);
        }

        if (n is LeafNode l)
        {
            return l.m * depth;
        }

        throw new Exception("What?");
    }

    private BaseNode DivEqually(ReadOnlySpan<CharFrequency> v)
    {
        if (v.Length < 1)
            throw new ArgumentException("Empty input");

        Span<double> sums = v.Length <= 64
            ? (stackalloc double[64]).Slice(0, v.Length)
            : new double[v.Length];
        
        sums[0] = v[0].F;
        for (int i=1; i<v.Length; ++i)
        {
            sums[i] = sums[i-1] + v[i].F;
        }

        return DivEqually(v, 0, v.Length, sums);
    }

    private BaseNode DivEqually(ReadOnlySpan<CharFrequency> v, int start, int end, Span<double> sums)
    {
        if (end - start <= 0)
        {
            throw new ArgumentException();
        }
        
        if (end - start == 1)
        {
            return new LeafNode {
                v = v[start],
                m = v[start].F
            };
        }
        else if (end - start == 2)
        {
            var l = DivEqually(v, start, start + 1, sums);
            var r = DivEqually(v, start + 1, end, sums);
            return new Node {
                left = l,
                right = r,
                m = l.m + r.m
            };
        }

        if (optimizationType == 1)
        {
            return DivRangeEquallyV1(v, start, end, sums);
        }
        else
        {
            return DivRangeEquallyV2(v, start, end, sums);
        }
    }

    private BaseNode DivRangeEquallyV1(ReadOnlySpan<CharFrequency> v, int start, int end, Span<double> sums)
    {
        var leftSum = (start > 0) ? sums[start - 1] : 0;
        var mid = (sums[end - 1] - leftSum) / 2;

        for (int i = start; i<end - 1; ++i)
        {
            var d1 = Math.Abs(mid - sums[i] + leftSum);
            var d2 = Math.Abs(mid - sums[end - 1] + sums[i]);

            var n1 = Math.Abs(mid - sums[i + 1] + leftSum);
            var n2 = Math.Abs(mid - sums[end - 1] + sums[i + 1]);

            // Is the distance to the center gets better?
            if (d1 + d2 <= n1 + n2)
            {
                var l = DivEqually(v, start, i + 1, sums);
                var r = DivEqually(v, i + 1, end, sums);
                return new Node {
                    left = l,
                    right = r,
                    m = l.m + r.m
                };
            }
        }

        throw new Exception("Terrible");
    }

    private BaseNode DivRangeEquallyV2(ReadOnlySpan<CharFrequency> v, int start, int end, Span<double> sums)
    {
        var leftSum = (start > 0) ? sums[start - 1] : 0;
        var mid = (sums[end - 1] - leftSum) / 2;

        for (int i = start; i<end - 1; ++i)
        {
            if (((sums[i]-leftSum) <= (sums[end - 1] - sums[i]) && (sums[i+1]-leftSum) > (sums[end - 1] - sums[i+1])) ||
                ((sums[i]-leftSum) > (sums[end - 1] - sums[i])))
            {
                var l = DivEqually(v, start, i + 1, sums);
                var r = DivEqually(v, i + 1, end, sums);
                return new Node {
                    left = l,
                    right = r,
                    m = l.m + r.m
                };
            }
        }

        throw new Exception("Terrible");
    }


    abstract class BaseNode : IEncodingNode
    {
        public double m;
    }

    sealed class Node : BaseNode, IEncodingTreeNode
    {
        public IEncodingNode Left => left;
        public IEncodingNode Right => right;
        public BaseNode left;
        public BaseNode right;
    }

    sealed class LeafNode : BaseNode, IEncodingLeafNode
    {
        public CharFrequency v;
        public char V => v.C;
    }

    sealed class BalancedByWeightEncoding : VarLenCharEncoding
    {
        public BalancedByWeightEncoding(IEncodingNode root)
            : base(root)
        {
        }
    }
}
