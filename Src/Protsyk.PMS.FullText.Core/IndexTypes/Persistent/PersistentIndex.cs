using System.IO;
using System.Linq;

using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core;

public class PersistentIndex : IFullTextIndex
{
    public static readonly string FileNameFields = "index-fields.pms";
    public static readonly string FileNameDictionary = "index-dictionary.pms";
    public static readonly string FileNamePostingLists = "index-postinglists.pms";
    public static readonly string FileNameInfo = "index-info.pms";
    public static readonly string FileNamePositions = "index-textpos.pms";
    public static readonly string FileNamePosIndex = "index-posindex.pms";
    public static readonly int PosIndexKeySize = 65;
    private readonly PersistentIndexName name;

    public PersistentIndex(PersistentIndexName name)
    {
        var folder = name.Folder;

        if (!PersistentIndexInfo.Exists(folder, FileNameInfo))
        {
            throw new InvalidOperationException("No index");
        }

        HeaderReader = new PersistentIndexInfo(folder, FileNameInfo);
        Header = HeaderReader.Read();
        if (Header is null)
        {
            throw new InvalidOperationException("No index");
        }

        VerifyHeader(name);
        var indexType = Header.Type.Split(' ');
        Dictionary = PersistentDictionaryFactory.Create(indexType[1], folder, FileNameDictionary, Header.MaxTokenSize, indexType[4]);
        PostingLists = PostingListIOFactory.CreateReader(indexType[3], folder, FileNamePostingLists);
        PosIndex = PersistentDictionaryFactory.Create(indexType[1], folder, FileNamePosIndex, PosIndexKeySize, indexType[4]);
        positionsReader = new DeltaVarIntListReader(folder, FileNamePositions);
        Fields = PersistentMetadataFactory.CreateStorage(indexType[2], folder, FileNameFields);
        this.name = name;
    }

    private void VerifyHeader(PersistentIndexName name)
    {
        var types = Header.Type.Split(' ');
        if (types[0] != nameof(PersistentIndex))
        {
            throw new InvalidOperationException("Index type and name mismatch");
        }

        if (name.DictionaryType != PersistentIndexName.DefaultValue && types[1] != name.DictionaryType)
        {
            throw new InvalidOperationException("Index type and name mismatch");
        }

        if (name.FieldsType != PersistentIndexName.DefaultValue && types[2] != name.FieldsType)
        {
            throw new InvalidOperationException("Index type and name mismatch");
        }

        if (name.PostingType != PersistentIndexName.DefaultValue && types[3] != name.PostingType)
        {
            throw new InvalidOperationException("Index type and name mismatch");
        }

        if (name.TextEncoding != PersistentIndexName.DefaultValue && types[4] != name.TextEncoding)
        {
            throw new InvalidOperationException("Index type and name mismatch");
        }
    }

    private PersistentIndexInfo HeaderReader { get; }

    public IFullTextIndexHeader Header { get; }

    public ITermDictionary Dictionary { get; }

    public IPostingLists PostingLists { get; }

    public ITermDictionary PosIndex { get; }

    private readonly DeltaVarIntListReader positionsReader;

    public IMetadataStorage<string> Fields { get; }

    public IEnumerable<DictionaryTerm> GetTerms(ITermMatcher matcher)
    {
        return Dictionary.GetTerms(matcher);
    }

    public IEnumerable<TextPosition> GetPositions(ulong docId, ulong fieldId)
    {
        var key = GetKeyForPositions('P', docId, fieldId);
        var matcher = new DfaTermMatcher(new SequenceMatcher<char>(key, false));
        var term = PosIndex.GetTerms(matcher).Single();
        var offset = -1;
        foreach (var pos in positionsReader.Get(term.Value.Offset))
        {
            if (offset == -1)
            {
                offset = (int)pos;
            }
            else
            {
                yield return TextPosition.P(offset - 1, (int)pos - offset);
                offset = -1;
            }
        }
    }

    public TextReader GetText(ulong docId, ulong fieldId)
    {
        var key = GetKeyForPositions('T', docId, fieldId);
        var matcher = new DfaTermMatcher(new SequenceMatcher<char>(key, false));
        var term = PosIndex.GetTerms(matcher).Single();
        return positionsReader.GetText(term.Value.Offset);
    }

    public ITermMatcher CompilePattern(string pattern)
    {
        using var compiler = new FullTextQueryCompiler(this);

        return compiler.CompilePattern(pattern);
    }

    public ISearchQuery Compile(string query)
    {
        using var compiler = new FullTextQueryCompiler(this);

        return compiler.Compile(query);
    }

    public void Dispose()
    {
        positionsReader?.Dispose();
        PosIndex?.Dispose();
        PostingLists?.Dispose();
        Dictionary?.Dispose();
        Fields?.Dispose();
        HeaderReader?.Dispose();
    }

    internal static string GetKeyForPositions(char prefix, ulong docId, ulong fieldId)
    {
        //return $"{docId}-{fieldId}";
        var key = Convert.ToString((int)docId, 2).PadLeft(32, '0') +
                  Convert.ToString(((int)(fieldId << 1)|(prefix == 'T' ? 0 : 1)), 2).PadLeft(32, '0');
        return key;
    }
}
