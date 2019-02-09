using System;
using System.IO;
using Protsyk.PMS.FullText.Core.Common.Compression;

namespace Protsyk.PMS.FullText.Core
{
    internal static class PersistentDictionaryFactory
    {
        public static ITermDictionary Create(string dictionaryType,
                                    string folder,
                                    string fileNameDictionary,
                                    int maxTokenLength,
                                    string encodingName)
        {
            if (dictionaryType == PersistentIndexName.DefaultValue || dictionaryType == PersistentDictionaryTst.Id)
            {
                return new PersistentDictionaryTst(folder, fileNameDictionary, maxTokenLength, TextEncodingFactory.GetByName(encodingName));
            }

            if (dictionaryType == PersistentDictionaryFst.Id)
            {
                return  new PersistentDictionaryFst(folder, fileNameDictionary, maxTokenLength, TextEncodingFactory.GetByName(encodingName));
            }

            throw new NotSupportedException($"Dictionary type {dictionaryType} is not supported");
        }

        public static IUpdateTermDictionary CreateWriter(string dictionaryType,
                                                        string folder,
                                                        string fileNameDictionary,
                                                        int maxTokenLength,
                                                        string encodingName)
        {
            return (IUpdateTermDictionary)Create(dictionaryType, folder, fileNameDictionary, maxTokenLength, encodingName);
        }

        public static string GetName(string fieldsType)
        {
            if (fieldsType == PersistentIndexName.DefaultValue)
            {
                return PersistentDictionaryTst.Id;
            }
            return fieldsType;
        }
    }
}
