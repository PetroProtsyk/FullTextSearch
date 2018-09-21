using System;
using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core.Common.Compression
{
    // Variation of Shannon Fano coding
    // https://en.wikipedia.org/wiki/Shannon%E2%80%93Fano_coding
    public class BalancedByWeightBuilder : VarLenCharEncodingBuilder
    {
        private readonly int optimiziationType;

        public BalancedByWeightBuilder()
            : this(1)
        {
        }

        public BalancedByWeightBuilder(int type)
        {
            optimiziationType = type;
        }

        protected override VarLenCharEncoding DoBuild()
        {
            return new BalancedByWeightEncoding(DivEqually(symbols));
        }

        private double Score(BaseNode n, int depth)
        {
            var d = n as Node;
            if (d != null)
            {
                return Score(d.left, depth + 1) + Score(d.right, depth + 1);
            }

            var l = n as LeafNode;
            if (l != null)
            {
                return l.m * depth;
            }

            throw new Exception("What?");
        }

        private BaseNode DivEqually(IList<CharFrequency> v)
        {
            if (v == null)
                throw new ArgumentNullException(nameof(v));

            if (v.Count < 1)
                throw new ArgumentException("Empty input");

            double[] sums = new double[v.Count];
            sums[0] = v[0].f;
            for (int i=1; i<v.Count; ++i)
            {
                sums[i] = sums[i-1] + v[i].f;
            }

            return DivEqually(v, 0, v.Count, sums);
        }

        private BaseNode DivEqually(IList<CharFrequency> v, int start, int end, double[] sums)
        {
            if (end - start <= 0)
            {
                throw new ArgumentException();
            }
            else if (end - start == 1)
            {
                return new LeafNode {
                    v = v[start],
                    m = v[start].f
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

            if (optimiziationType == 1)
            {
                return DivRangeEquallyV1(v, start, end, sums);
            }
            else
            {
                return DivRangeEquallyV2(v, start, end, sums);
            }
        }

        private BaseNode DivRangeEquallyV1(IList<CharFrequency> v, int start, int end, double[] sums)
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

        private BaseNode DivRangeEquallyV2(IList<CharFrequency> v, int start, int end, double[] sums)
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


        class BaseNode : IEncodingNode
        {
            public double m;
        }

        class Node : BaseNode, IEncodingTreeNode
        {
            public IEncodingNode Left => left;
            public IEncodingNode Right => right;
            public BaseNode left;
            public BaseNode right;
        }

        class LeafNode : BaseNode, IEncodingLeafNode
        {
            public CharFrequency v;
            public char V => v.c;
        }

        class BalancedByWeightEncoding : VarLenCharEncoding
        {
            public BalancedByWeightEncoding(IEncodingNode root)
                : base(root)
            {
            }
        }
    }
}
