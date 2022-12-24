using System;

namespace Protsyk.PMS.FullText.Core;

public interface IMetadataStorage<T> : IDisposable
{
    T GetMetadata(ulong id);

    void SaveMetadata(ulong id, T data);
}
