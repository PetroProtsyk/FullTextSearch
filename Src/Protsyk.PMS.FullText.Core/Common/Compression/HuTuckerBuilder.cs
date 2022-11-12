using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core.Common.Compression
{
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
                combinationList.Add(new TreeLeaf() {V = symbol.c, w = symbol.f});
            }

            var result = combinationList.PerformGarciaWachsCombination();
            SetLevels(result, 0);
            var final = Recombination(combinationList.list);
            return new HuTuckerEncoding(final);
        }

        private void SetLevels(ListItem result, int level)
        {
            result.l = level;

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
                if (stack.Count < 2 || (index < queue.Count && stack[stack.Count - 1].l != stack[stack.Count - 2].l))
                {
                    stack.Add(new TreeLeaf() {l = queue[index].l, V = queue[index].V, w = queue[index].w});
                    ++index;
                }
                else
                {
                    var l1 = stack[stack.Count - 1];
                    var l2 = stack[stack.Count - 2];

                    stack.RemoveAt(stack.Count - 1);
                    stack.RemoveAt(stack.Count - 1);

                    stack.Add(
                        new TreeNode()
                        {
                            Left = l2,
                            Right = l1,
                            w = l1.w + l2.w,
                            l = l2.l - 1
                        });

                    if (l2.l - 1 == 0)
                        break;
                }
            }

            return (TreeNode) stack[0];
        }

        class ListItem : IEncodingNode
        {
            public double w { get; set; }
            public int l { get; set; }

            public ListItem Previous { get; set; }
            public ListItem Next { get; set; }

            public virtual string ToString(string ofset, string code)
            {
                return ToString();
            }
        }

        class TreeLeaf : ListItem, IEncodingLeafNode
        {
            public char V { get; set; }

            public override string ToString(string ofset, string code)
            {
                return $"{V}-{code}";
            }
        }

        class TreeNode : ListItem, IEncodingTreeNode
        {
            public ListItem Left { get; set; }
            public ListItem Right { get; set; }

            IEncodingNode IEncodingTreeNode.Left => Left;

            IEncodingNode IEncodingTreeNode.Right => Right;
        }

        class HuTuckerEncoding : VarLenCharEncoding
        {
            internal HuTuckerEncoding(ListItem root)
                : base(root)
            {
            }
        }

    }
}
