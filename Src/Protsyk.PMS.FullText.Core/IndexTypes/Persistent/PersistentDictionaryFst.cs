using System;
using System.Collections.Generic;
using System.IO;

using Protsyk.PMS.FullText.Core.Automata;
using Protsyk.PMS.FullText.Core.Collections;
using Protsyk.PMS.FullText.Core.Common.Compression;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core;

public class PersistentDictionaryFst : ITermDictionary, IUpdateTermDictionary, IDisposable
{
    #region Fields
    public static string Id = "FST";

    private readonly FST<int> fst;
    private readonly int maxTokenByteLength;
    private readonly IPersistentStorage storage;
    private readonly ITextEncoding encoding;

    private Update update;
    #endregion

    #region Constructor
    public PersistentDictionaryFst(string folder,
                                string fileNameDictionary,
                                int maxTokenLength,
                                ITextEncoding encoding)
        : this(new FileStorage(Path.Combine(folder, fileNameDictionary)),
               maxTokenLength,
               encoding)
    {
    }

    public PersistentDictionaryFst(IPersistentStorage storage,
                                int maxTokenLength,
                                ITextEncoding encoding)
    {
        this.maxTokenByteLength = encoding.GetMaxEncodedLength(maxTokenLength);
        this.storage = storage;
        this.encoding = encoding;
        if (storage.Length > 0)
        {
            var buffer = new byte[storage.Length];
            storage.ReadAll(0, buffer);
            this.fst = FST<int>.FromBytesCompressed(buffer, FSTVarIntOutput.Instance);
        }
    }
    #endregion

    #region ITermDictionary
    public IEnumerable<DictionaryTerm> GetTerms(ITermMatcher matcher)
    {
        // var decodingMatcher = encoding.CreateMatcher(matcher.ToDfaMatcher(), maxTokenByteLength);

        foreach (var term in fst.Match(matcher.ToDfaMatcher()))
        {
            yield return GetTerm(term);
        }
    }

    public DictionaryTerm GetTerm(string term)
    {
        int offset = 0;
        if (!fst.TryMatch(term, out offset))
        {
            throw new InvalidOperationException();
        }

        if (offset < 0)
        {
            throw new InvalidOperationException();
        }

        return new DictionaryTerm(term, new PostingListAddress(offset));
    }
    #endregion

    #region IUpdateTermDictionary

    class Update : IUpdate
    {
        private List<string> input = new List<string>();
        private List<int> output = new List<int>();
        private readonly IPersistentStorage storage;

        public Update(IPersistentStorage storage)
        {
            this.storage = storage;
        }

        public void Commit()
        {
        }

        public void Dispose()
        {
            if (input != null)
            {
                var fst = new FSTBuilder<int>(FSTVarIntOutput.Instance).FromList(input.ToArray(), output.ToArray());
                Validate(fst); //TODO: Optional
                {
                    var fstData = fst.GetBytesCompressed();
                    storage.WriteAll(0, fstData);
                }
                input = null;
                output = null;
            }
        }

        private void Validate(FST<int> fst)
        {
            for (int i = 0; i < input.Count; ++i)
            {
                if (fst.TryMatch(input[i], out var offset))
                {
                    if (offset != output[i])
                    {
                        throw new Exception($"Bad output {input[i]} {offset} != {output[i]}");
                    }
                }
                else
                {
                    throw new Exception($"No match {input[i]}");
                }
            }
        }

        public void Rollback()
        {
            input.Clear();
            output.Clear();
        }

        public void AddTerm(string term, PostingListAddress address, Action<PostingListAddress> onDuplicate)
        {
            input.Add(term);
            output.Add((int)address.Offset);
        }
    }

    public IUpdate BeginUpdate()
    {
        if (fst != null)
        {
            throw new NotSupportedException("It is not possible to update FST");
        }

        update ??= new Update(storage);

        return update;
    }

    public void AddTerm(string term, PostingListAddress address, Action<PostingListAddress> onDuplicate)
    {
        update.AddTerm(term, address, onDuplicate);
    }
    #endregion

    #region IDisposable
    public void Dispose()
    {
        storage?.Dispose();
    }
    #endregion
}
