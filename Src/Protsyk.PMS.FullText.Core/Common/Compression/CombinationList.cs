using System.Globalization;
using System.Text;

namespace Protsyk.PMS.FullText.Core.Common.Compression;

public partial class HuTuckerBuilder
{
    class CombinationList
    {
        ListItem First { get; set; }
        ListItem Last { get; set; }
        public List<TreeLeaf> list = new List<TreeLeaf>();

        public void Add(TreeLeaf item)
        {
            list.Add(item);

            if (First is null && Last is null)
            {
                First = item;
                Last = item;

                item.Next = null;
                item.Previous = null;

                return;
            }

            Last.Next = item;

            item.Previous = Last;
            Last = item;
        }

        public TreeNode Combine(ListItem left, ListItem right)
        {
            var node = new TreeNode();
            node.Left = left;
            node.Right = right;
            node.W = left.W + right.W;

            if (left.Next != right)
            {
                node.Previous = left.Previous;
                node.Next = left.Next;

                if (left.Next != null)
                    left.Next.Previous = node;

                if (left.Previous != null)
                {
                    left.Previous.Next = node;
                }

                if (right.Previous != null)
                {
                    right.Previous.Next = right.Next;
                }

                if (right.Next != null)
                {
                    right.Next.Previous = right.Previous;
                }
            }
            else
            {

                node.Previous = left.Previous;
                node.Next = right.Next;

                if (left.Previous != null)
                {
                    left.Previous.Next = node;
                }

                if (right.Next != null)
                {
                    right.Next.Previous = node;
                }
            }

            right.Next = null;
            right.Previous = null;

            left.Next = null;
            left.Previous = null;

            if (node.Previous is null)
                First = node;

            if (node.Next is null)
                Last = node;

            return node;
        }

        public TreeNode PerformCombination()
        {
            while (First != Last)
            {
                var r1 = First;

                var minW = double.MaxValue;
                var minV1 = default(ListItem);
                var minV2 = default(ListItem);

                while (r1 != null)
                {
                    if (r1.Next is null)
                        break;

                    var r2 = r1.Next;
                    do
                    {
                        if (r2 is null)
                            break;
                        if (r2 is TreeLeaf)
                            break;

                        if (minW > r1.W + r2.W)
                        {
                            minW = r1.W + r2.W;

                            minV1 = r1;
                            minV2 = r2;
                        }

                        r2 = r2.Next;
                    } while (!(r2 is TreeLeaf));

                    if (r2 != null)
                    {
                        if (minW > r1.W + r2.W)
                        {
                            minW = r1.W + r2.W;

                            minV1 = r1;
                            minV2 = r2;
                        }
                    }

                    r1 = r1.Next;
                }

                Combine(minV1, minV2);
            }

            return (TreeNode) First;
        }


        public TreeNode PerformGarciaWachsCombination()
        {
            while (First != Last)
            {
                var r1 = First.Next.Next;

                if (r1 is null)
                {
                    Combine(First, First.Next);
                    break;
                }

                while (r1 != null)
                {

                    if (r1.Next is null || ((r1.Previous.W <= r1.Next.W) && (r1.Previous.Previous.W > r1.W)))
                    {

                        var newNode = Combine(r1.Previous, r1);
                        while (newNode.Previous != null && newNode.Previous.W <= newNode.W)
                            MoveLeft(newNode);

                        r1 = First.Next;
                    }

                    r1 = r1.Next;
                }
            }

            return (TreeNode) First;
        }


        private void MoveLeft(TreeNode newNode)
        {
            var n1 = newNode.Previous;
            var n2 = newNode.Previous.Previous;
            var n3 = newNode.Next;

            newNode.Previous = n2;
            newNode.Next = n1;

            if (n2 != null)
                n2.Next = newNode;

            n1.Previous = newNode;
            n1.Next = n3;

            if (n3 != null)
                n3.Previous = n1;
            else
                Last = n1;

            if (newNode.Previous is null)
                First = newNode;

            if (newNode.Next is null)
                Last = newNode;
        }


        public override string ToString()
        {
            var sb = new StringBuilder();
            for (var r = First; r != null; r = r.Next)
            {
                if (r is TreeLeaf)
                    sb.Append(r.W);
                else
                    sb.Append(CultureInfo.InvariantCulture, $"({r.W})");

                if (r.Next != null)
                {
                    if (r.Next.Previous != r)
                        sb.Append("<=!!!");

                    sb.Append("=>");
                }
            }

            return sb.ToString();
        }
    }
}
