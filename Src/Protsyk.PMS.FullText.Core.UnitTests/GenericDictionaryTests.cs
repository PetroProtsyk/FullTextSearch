using System.Collections.Generic;
using System.Linq;

namespace Protsyk.PMS.FullText.Core.UnitTests;

internal class GenericDictionaryTests
{
    public static T MakeDefault<T>() where T : IDictionary<int, string>, new()
    {
        var tree = new T();
        tree.Add(1, "Petro");
        tree.Add(4, "Max");
        tree.Add(2, "Sophie");
        tree.Add(3, "Mariya");

        tree.Add(101, "Stranger");
        tree.Add(102, "Colonel");
        tree.Add(103, "Boss");
        tree.Add(56, "Monkey");
        tree.Add(156, "Apple");
        tree.Add(99, "Mango");
        tree.Add(110, "Jungle");
        return tree;
    }

    public static void TheFirstTest<T>() where T : IDictionary<int, string>, new()
    {
        var tree = MakeDefault<T>();

        Assert.True(tree.ContainsKey(1));
        Assert.True(tree.ContainsKey(3));
        Assert.True(tree.ContainsKey(101));
        Assert.True(tree.ContainsKey(156));
    }

    public static void TheSecondTest<T>(int n) where T : IDictionary<int, string>, new()
    {
        var rnd = Enumerable.Range(1, n).ToArray().Shuffle();
        var tree = new T();
        foreach (var r in rnd)
        {
            tree.Add(r, r.ToString());
        }

        foreach (var r in rnd)
        {
            Assert.True(tree.ContainsKey(r));
        }

        for (int i = -1000; i < 0; i++)
        {
            Assert.False(tree.ContainsKey(i));
        }
    }

    public static void TheOrderByTest<T>(int n) where T : IDictionary<int, string>, new()
    {
        var rnd = Enumerable.Range(1, n).ToArray().Shuffle();
        var tree = new T();
        foreach (var r in rnd)
        {
            tree.Add(r, r.ToString());
        }

        var orderTree = tree.Select(k => k.Key).ToArray();
        var orderArray = rnd.OrderBy(r => r).ToArray();

        Assert.Equal(orderArray.Length, orderTree.Length);
        for (int i = 0; i < orderTree.Length; i++)
        {
            Assert.Equal(orderArray[i], orderTree[i]);
        }
    }


    public static void TheRemoveTest<T>() where T : IDictionary<int, string>, new()
    {
        var rnd = Enumerable.Range(1, 100).ToArray();
        var tree = new T();
        foreach (var r in rnd)
        {
            tree.Add(r, r.ToString());
        }

        // This removal causes concatenation
        tree.Remove(72);

        // This removal causes redistribution
        tree.Remove(50);

        var orderTree = tree.Select(k => k.Key).ToArray();
        var orderArray = rnd.Where(r => r != 72 && r != 50).ToArray();

        Assert.Equal(orderArray.Length, orderTree.Length);
        for (int i = 0; i < orderTree.Length; i++)
        {
            Assert.Equal(orderArray[i], orderTree[i]);
        }
    }

    public static void TheRemoveTestFull<T>(int n) where T : IDictionary<int, string>, new()
    {
        var rnd = Enumerable.Range(1, n).ToArray();
        var tree = new T();
        foreach (var r in rnd)
        {
            tree.Add(r, r.ToString());
        }

        var deleteSequence = rnd.Shuffle().ToArray();

        while (deleteSequence.Length > 0)
        {
            var orderTree = tree.Select(k => k.Key).ToArray();
            var orderArray = deleteSequence.OrderBy(i => i).ToArray();

            Assert.Equal(orderArray.Length, orderTree.Length);
            for (int i = 0; i < orderTree.Length; i++)
            {
                Assert.Equal(orderArray[i], orderTree[i]);
            }

            tree.Remove(deleteSequence[0]);
            deleteSequence = deleteSequence.Skip(1).ToArray();
        }
    }

    public static void TheDictionaryTest<T>() where T : IDictionary<int, string>, new()
    {
        var tree = MakeDefault<T>();

        tree[1984] = "Petro";
        tree[2012] = "Magda";
        tree[2012] = "Sophie";

        Assert.Equal("Petro", tree[1984]);
        Assert.Equal("Sophie", tree[2012]);
    }
}
