namespace Protsyk.PMS.FullText.Core.Common.Compression;

public partial class HuTuckerBuilder : VarLenCharEncodingBuilder
{
    private readonly CombinationList combinationList;

    public HuTuckerBuilder()
    {
        combinationList = new CombinationList();
    }

    protected override VarLenCharEncoding DoBuild()
    {
        foreach(var symbol in symbols)
        {
            combinationList.Add(new TreeLeaf { V = symbol.C, W = symbol.F });
        }

        var result = combinationList.PerformGarciaWachsCombination();
        SetLevels(result, 0);
        var final = Recombination(combinationList.list);
        return new HuTuckerEncoding(final);
    }

    private void SetLevels(ListItem result, int level)
    {
        result.L = level;

        if (result is TreeNode node)
        {
            SetLevels(node.Left, level + 1);
            SetLevels(node.Right, level + 1);
        }
    }

    private TreeNode Recombination(List<TreeLeaf> queue)
    {
        var stack = new List<ListItem>();
        var index = 0;
        while (true)
        {
            if (stack.Count < 2 || (index < queue.Count && stack[^1].L != stack[^2].L))
            {
                stack.Add(new TreeLeaf {
                    L = queue[index].L,
                    V = queue[index].V,
                    W = queue[index].W 
                });
                ++index;
            }
            else
            {
                var l1 = stack[^1];
                var l2 = stack[^2];

                stack.RemoveAt(stack.Count - 1);
                stack.RemoveAt(stack.Count - 1);

                stack.Add(new TreeNode {
                    Left = l2,
                    Right = l1,
                    W = l1.W + l2.W,
                    L = l2.L - 1
                });

                if (l2.L - 1 == 0)
                    break;
            }
        }

        return (TreeNode)stack[0];
    }

    private abstract class ListItem : IEncodingNode
    {
        public double W { get; set; }
        
        public int L { get; set; }

        public ListItem Previous { get; set; }

        public ListItem Next { get; set; }

        public virtual string ToString(string offset, string code)
        {
            return ToString();
        }
    }

    private sealed class TreeLeaf : ListItem, IEncodingLeafNode
    {
        public char V { get; set; }

        public override string ToString(string offset, string code)
        {
            return $"{V}-{code}";
        }
    }

    private sealed class TreeNode : ListItem, IEncodingTreeNode
    {
        public ListItem Left { get; set; }

        public ListItem Right { get; set; }

        IEncodingNode IEncodingTreeNode.Left => Left;

        IEncodingNode IEncodingTreeNode.Right => Right;
    }

    private sealed class HuTuckerEncoding : VarLenCharEncoding
    {
        internal HuTuckerEncoding(ListItem root)
            : base(root)
        {
        }
    }
}
