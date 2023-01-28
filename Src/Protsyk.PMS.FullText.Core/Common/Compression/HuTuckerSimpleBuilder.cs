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
        var nodes = Enumerable.Range(0, input.Count).Select(v => new Leaf { Index = v, C = input[v].C, Frequency = input[v].F }).Cast<Node>().ToArray();

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
                Left = nodes[m1.Item1],
                Right = nodes[m2.Item1],
                Frequency = f1 + f2
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
                Index = i,
                C = input[i].C, 
                Frequency = input[i].F 
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
                Left = b1.Value,
                Right = b2.Value,
                Frequency = bestF
            });
            list.Remove(b1);
            list.Remove(b2);
        }

        // Label each input item with the depth in the tree
        var depth = new int[input.Length];
        var s = new Stack<ValueTuple<Node, int>>();
        s.Push(new(list.Single(), 0));
        while (s.Count > 0)
        {
            var current = s.Pop();

            if (current.Item1 is Leaf leaf)
            {
                depth[leaf.Index] = current.Item2;
            }
            else if (current.Item1 is MergeNode merge)
            {
                s.Push(new(merge.Left, current.Item2 + 1));
                s.Push(new(merge.Right, current.Item2 + 1));
            }
        }

        var result = new ValueTuple<int, int>[input.Length];
        for (int i = 0; i < input.Length; ++i)
        {
            result[i] = new(i, depth[i]);
        }
        return result;
    }

    private static int GetFrequency(Node node)
    {
        return node switch
        {
            Leaf leaf       => leaf.Frequency,
            MergeNode merge => merge.Frequency,
            _               => throw new Exception("Unexpected node")
        };
    }

    abstract class Node : IEncodingNode
    {
        public int Frequency { get; set; }
    }

    sealed class MergeNode : Node, IEncodingTreeNode
    {
        public Node Left { get; set; }

        public Node Right { get; set; }

        IEncodingNode IEncodingTreeNode.Left => Left;

        IEncodingNode IEncodingTreeNode.Right => Right;
    }

    sealed class Leaf : Node, IEncodingLeafNode
    {
        public char V => C;

        public char C {get; set;}

        public int Index { get; set; }
    }

    sealed class HuTuckerEncoding : VarLenCharEncoding
    {
        internal HuTuckerEncoding(Node root)
            : base(root)
        {
        }
    }
}
