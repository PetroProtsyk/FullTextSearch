using System;
using System.Collections.Generic;
using System.Text;

namespace Protsyk.PMS.FullText.Core.Common.Compression;

public class TextEncodingFactory
{
    private static SortedDictionary<char, int> latinFrequencies = new SortedDictionary<char, int>()
    {
        { '.', 5 },
        { ',', 5 },
        { '!', 2 },
        { '?', 2 },
        { ':', 1 },
        { '-', 5 },
        { 'a', 889 },
        { 'b', 158 },
        { 'c', 399 },
        { 'd', 277 },
        { 'e', 1138 },
        { 'f', 93 },
        { 'g', 121 },
        { 'h', 69 },
        { 'i', 1144 },
        { 'j', 114 },
        { 'k', 100 },
        { 'l', 315 },
        { 'm', 538 },
        { 'n', 628 },
        { 'o', 540 },
        { 'p', 303 },
        { 'q', 151 },
        { 'r', 667 },
        { 's', 760 },
        { 't', 800 },
        { 'u', 846 },
        { 'v', 96 },
        { 'w', 90 },
        { 'x', 60 },
        { 'y', 7 },
        { 'z', 10 }
    };

    public static string GetName(string textEncoding)
    {
        if (textEncoding == "Default")
        {
            return TextEncoding.Default.GetName();
        }

        return textEncoding;
    }

    public static ITextEncoding GetByName(string textEncoding)
    {
        var name = GetName(textEncoding);

        switch (textEncoding)
        {
            case "LatinHuffman": 
                return new VarLenEncoding(VarLenCharEncoding.FromFrequency<HuffmanEncodingBuilder>(latinFrequencies, true));
            case "LatinHuTucker": 
                return new VarLenEncoding(VarLenCharEncoding.FromFrequency<HuTuckerBuilder>(latinFrequencies, true));
            case "LatinHuTuckerBasic": 
                return new VarLenEncoding(VarLenCharEncoding.FromFrequency<HuTuckerSimpleBuilder>(latinFrequencies, false));
            case "LatinBalanced":
                return new VarLenEncoding(VarLenCharEncoding.FromFrequency<BalancedByWeightBuilder>(latinFrequencies, true));
        }

        var encoding = Encoding.GetEncoding(name);

        if (encoding != null)
        {
            return new TextEncoding(encoding);
        }

        throw new Exception("Encoding is not defined");
    }

}
