using System;
using System.Collections.Generic;
using Protsyk.PMS.FullText.Core.Collections;
using Xunit;

namespace Protsyk.PMS.FullText.Core.UnitTests
{
    public class BtreeTests
    {
        [Theory]
        [InlineData(1000)]
        public void BtreeTests_Base(int size)
        {
            GenericDictionaryTests.TheFirstTest<Btree<int, string>>();
            GenericDictionaryTests.TheSecondTest<Btree<int, string>>(size);
            GenericDictionaryTests.TheOrderByTest<Btree<int, string>>(size);
            GenericDictionaryTests.TheRemoveTest<Btree<int, string>>();
            GenericDictionaryTests.TheRemoveTestFull<Btree<int, string>>(size);
            GenericDictionaryTests.TheDictionaryTest<Btree<int, string>>();
        }
    }
}
