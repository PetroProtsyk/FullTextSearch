using System;
using System.Collections.Generic;
using System.IO;

namespace Protsyk.PMS.FullText.Core;

public interface IIndexBuilder : IDisposable
{
    void Start();
    void AddText(string text, string metadata);
    void AddFile(string fileName, string metadata);
    void AddCompound(IInputDocument document);
    IndexBuilderStatistics StopAndWait();
}

public class IndexBuilderStatistics
{
    public long Terms { get; set; }

    public long Occurrences { get; set; }
}

public interface IInputDocument
{
    string Metadata {get;}

    IReadOnlyList<IInputText> Fields {get;}
}

public interface IInputText
{
    TextReader GetTextReader();
}
