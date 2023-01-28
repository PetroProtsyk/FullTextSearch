using System.Linq;
using System.Runtime.InteropServices;

namespace Protsyk.PMS.FullText.Core.Common.Compression;

// http://www-math.mit.edu/~shor/PAM/hu-tucker_algorithm.html
public class HuTuckerSimpleBuilder : VarLenCharEncodingBuilder
{
    protected override VarLenCharEncoding DoBuild()
    {
        var input = symbols;
        var combine = Combine(CollectionsMarshal.AsSpan(input)).ToArray();

        // Rebuild tree based on the depth
        var nodes = Enumerable.Range(0, input.Count).Select(v => new Leaf { index = v, c = input[v].c, freq = input[v].f }).Cast<Node>().ToArray();

        // TODO: Use heap
        var heap = new SortedSet<ValueTuple<int, int>>(combine,
                                                        Comparer<(int, int)>.Create((x, y) =>
                                                        {
                                                            if (x.Item2 == y.Item2)
                                                            {
                                                                return x.Item1 - y.Item1;
                                                            }

                                                            return y.Item2 - x.Item2;
                                                        }));

        while (heap.Count > 1)
        {
            var m1 = heap.Min;
            heap.Remove(m1);

            var m2 = heap.Min;
            heap.Remove(m2);

            if (m1.Item2 != m2.Item2)
            {
                throw new Exception("What?");
            }

            var f1 = GetFrequency(nodes[m1.Item1]);
            var f2 = GetFrequency(nodes[m2.Item1]);

            nodes[m1.Item1] = new MergeNode
            {
                left = nodes[m1.Item1],
                right = nodes[m2.Item1],
                freq = f1 + f2
            };

            heap.Add(new ValueTuple<int, int>(m1.Item1, m1.Item2 - 1));
        }

        return new HuTuckerEncoding(nodes[heap.Single().Item1]);
    }

    private ValueTuple<int, int>[] Combine(ReadOnlySpan<CharFrequency> input)
    {
        if (input == null || input.Length < 2)
        {
            throw new ArgumentException("Input should have at least two items", nameof(input));
        }

        // Merge compatible blocks b1 and b2 with the lowest combined frequency until all blocks are merged
        // Blocks are compatible if there are no original blocks (leaves) left between them
        var list = new LinkedList<Node>();

        for (int i = 0; i < input.Length; i++)
        {
            list.AddLast(new Leaf { 
                index = i,
                c = input[i].c, 
                freq = input[i].f 
            });
        }

        while (list.Count > 1)
        {
            LinkedListNode<Node> b1 = null;
            LinkedListNode<Node> b2 = null;
            int bestF = int.MaxValue;

            var n1 = list.First;

            while (n1 != null)
            {
                var n2 = n1.Next;
                while (n2 != null)
                {
                    var f1 = GetFrequency(n1.Value);
                    var f2 = GetFrequency(n2.Value);

                    if (f1 + f2 < bestF)
                    {
                        bestF = f1 + f2;
                        b1 = n1;
                        b2 = n2;
                    }

                    if (n2.Value is Leaf)
                    {
                        break;
                    }

                    n2 = n2.Next;
                }

                n1 = n1.Next;
            }

            list.AddBefore(b1, new MergeNode
            {
                left = b1.Value,
                right = b2.Value,
                freq = bestF
            });
            list.Remove(b1);
            list.Remove(b2);
        }

        // Label each input item with the depth in the tree
        var depth = new int[input.Length];
        var s = new Stack<ValueTuple<Node, int>>();
        s.Push(new ValueTuple<Node, int>(list.Single(), 0));
        while (s.Count > 0)
        {
            var current = s.Pop();

            if (current.Item1 is Leaf leaf)
            {
                depth[leaf.index] = current.Item2;
            }

            if (current.Item1 is MergeNode merge)
            {
                s.Push(new ValueTuple<Node, int>(merge.left, current.Item2 + 1));
                s.Push(new ValueTuple<Node, int>(merge.right, current.Item2 + 1));
            }
        }

        var result = new ValueTuple<int, int>[input.Length];
        for (int i = 0; i < input.Length; ++i)
        {
            result[i] = new ValueTuple<int, int>(i, depth[i]);
        }
        return result;
    }

    private int GetFrequency(Node node)
    {
        if (node is Leaf leaf)
        {
            return leaf.freq;
        }

        if (node is MergeNode merge)
        {
            return merge.freq;
        }

        throw new Exception();
    }

    abstract class Node : IEncodingNode
    {
        public int freq { get; set; }
    }

    sealed class MergeNode : Node, IEncodingTreeNode
    {
        public IEncodingNode Left => left;
        public IEncodingNode Right => right;

        public Node left { get; set; }
        public Node right { get; set; }
    }

    sealed class Leaf : Node, IEncodingLeafNode
    {
        public char V => c;

        public char c {get; set;}

        public int index { get; set; }
    }

    sealed class HuTuckerEncoding : VarLenCharEncoding
    {
        internal HuTuckerEncoding(Node root)
            : base(root)
        {
        }
    }
}
