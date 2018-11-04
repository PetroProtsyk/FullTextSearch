using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Protsyk.PMS.FullText.Core.Common.Compression;
using Xunit;

namespace Protsyk.PMS.FullText.Core.Automata.UnitTests
{
    public class FiniteStateTransducerTests
    {
        [Fact]
        public void SimpleConstructionWithIntOutputs()
        {
            var inputs = new string[]
            {
                "a banana",
                "a lemon",
                "a mandarine",
                "a mango",
                "an apple",
                "an orange",
            };

            var outputs = new int[]
            {
                1,
                2,
                3,
                -2,
                15,
                8
            };

            var fst = new FSTBuilder<int>(FSTVarIntOutput.Instance).FromList(inputs, outputs);
            Verify(fst, inputs, outputs);

            var fst1 = FST<int>.FromBytes(fst.GetBytes(), FSTVarIntOutput.Instance);
            Verify(fst1, inputs, outputs);

            var fst2 = FST<int>.FromBytesCompressed(fst.GetBytesCompressed(), FSTVarIntOutput.Instance);
            Verify(fst2, inputs, outputs);
        }

        [Fact]
        public void ConstructionWithIntOutputs()
        {
            var inputs = new string[]
            {
                "a",
                "ab",
                "abilities",
                "ability",
            };

            var outputs = new int[]
            {
                4,
                3134,
                7488,
                1580,
            };

            var fst = new FSTBuilder<int>(FSTVarIntOutput.Instance).FromList(inputs, outputs);
            Verify(fst, inputs, outputs);

            var fst1 = FST<int>.FromBytes(fst.GetBytes(), FSTVarIntOutput.Instance);
            Verify(fst1, inputs, outputs);

            var fst2 = FST<int>.FromBytesCompressed(fst.GetBytesCompressed(), FSTVarIntOutput.Instance);
            Verify(fst2, inputs, outputs);
        }


        [Fact]
        public void SimpleConstructionWithStringOutputs()
        {
            var inputs = new string[]
            {
                "a banana",
                "a lemon",
                "a mandarine",
                "a mango",
                "an apple",
                "an orange",
            };

            var outputs = new string[]
            {
                "one",
                "two",
                "three",
                "minusone",
                "minustwo",
                "minuseight"
            };

            var fst = new FSTBuilder<string>(FSTStringOutput.Instance).FromList(inputs, outputs);
            Verify(fst, inputs, outputs);

            var fst1 = FST<string>.FromBytes(fst.GetBytes(), FSTStringOutput.Instance);
            Verify(fst1, inputs, outputs);

            var fst2 = FST<string>.FromBytesCompressed(fst.GetBytesCompressed(), FSTStringOutput.Instance);
            Verify(fst2, inputs, outputs);
        }

        private void Verify<T>(FST<T> fst, string[] inputs, T[] outputs)
        {
            for (int i=0; i<inputs.Length; ++i)
            {
                Assert.True(fst.TryMatch(inputs[i], out var v));
                Assert.Equal(outputs[i], v);
            }
        }
    }
}